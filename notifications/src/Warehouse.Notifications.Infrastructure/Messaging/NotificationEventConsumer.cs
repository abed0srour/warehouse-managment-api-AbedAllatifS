using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Warehouse.Domain.Events;
using Warehouse.Notifications.Application.EventProcessing;

namespace Warehouse.Notifications.Infrastructure.Messaging
{
    public class NotificationEventConsumer : BackgroundService
    {
        private const string StockLowRoutingKey = "stock.low";
        private const string FileUploadedRoutingKey = "file.uploaded";
        private const string DeadLetterExchangeSuffix = ".dlx";
        private const string DeadLetterQueueSuffix = ".dlq";
        private const string RetryCountHeader = "x-retry-count";
        private const int MaxRetries = 3;

        private static readonly TimeSpan[] RetryBackoff =
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4)
        };

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        private readonly RabbitMqOptions _options;
        private readonly ILogger<NotificationEventConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public NotificationEventConsumer(
            IOptions<RabbitMqOptions> options,
            ILogger<NotificationEventConsumer> logger,
            IServiceScopeFactory scopeFactory)
        {
            _options = options.Value;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndConsumeAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "NotificationEventConsumer could not reach RabbitMQ at {Host}:{Port}. Retrying in {RetrySeconds}s. Reason: {ExceptionType}.",
                        _options.Host, _options.Port, RetryDelay.TotalSeconds, ex.GetType().Name);
                }

                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                DispatchConsumersAsync = true,
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var deadLetterExchange = _options.Exchange + DeadLetterExchangeSuffix;
            var deadLetterQueue = _options.Queue + DeadLetterQueueSuffix;

            channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);

            channel.ExchangeDeclare(deadLetterExchange, ExchangeType.Fanout, durable: true);
            channel.QueueDeclare(deadLetterQueue, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(deadLetterQueue, deadLetterExchange, routingKey: string.Empty);

            var queueArgs = new Dictionary<string, object> { ["x-dead-letter-exchange"] = deadLetterExchange };
            channel.QueueDeclare(_options.Queue, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
            channel.QueueBind(_options.Queue, _options.Exchange, StockLowRoutingKey);
            channel.QueueBind(_options.Queue, _options.Exchange, FileUploadedRoutingKey);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += (_, eventArgs) => OnMessageReceivedAsync(channel, eventArgs);

            channel.BasicConsume(_options.Queue, autoAck: false, consumer);

            _logger.LogInformation(
                "NotificationEventConsumer connected to RabbitMQ at {Host}:{Port}, listening on queue '{Queue}'.",
                _options.Host, _options.Port, _options.Queue);

            var connectionLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.ConnectionShutdown += (_, _) => connectionLost.TrySetResult();
            using var registration = stoppingToken.Register(() => connectionLost.TrySetResult());

            await connectionLost.Task;
        }

        private async Task OnMessageReceivedAsync(IModel channel, BasicDeliverEventArgs eventArgs)
        {
            var routingKey = eventArgs.RoutingKey;

            try
            {
                IntegrationEvent? @event = routingKey switch
                {
                    StockLowRoutingKey => JsonSerializer.Deserialize<StockLowDetected>(eventArgs.Body.Span),
                    FileUploadedRoutingKey => JsonSerializer.Deserialize<WarehouseFileUploaded>(eventArgs.Body.Span),
                    _ => null
                };

                if (@event is null)
                {
                    _logger.LogWarning(
                        "Discarding message with unrecognized routing key '{RoutingKey}' (delivery tag {DeliveryTag}).",
                        routingKey, eventArgs.DeliveryTag);
                    channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(new ProcessIntegrationEventCommand(@event, routingKey), CancellationToken.None);

                _logger.LogInformation(
                    "Event {EventId} on routing key '{RoutingKey}' processed with result {Result}.",
                    @event.EventId, routingKey, result);

                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex, "Failed to deserialize message on routing key '{RoutingKey}' (delivery tag {DeliveryTag}). Discarding.",
                    routingKey, eventArgs.DeliveryTag);
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogInformation(
                    "Unique constraint prevented a duplicate insert for routing key '{RoutingKey}' (delivery tag {DeliveryTag}).",
                    routingKey, eventArgs.DeliveryTag);
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                await HandleTransientFailureAsync(channel, eventArgs, ex);
            }
        }

        private async Task HandleTransientFailureAsync(IModel channel, BasicDeliverEventArgs eventArgs, Exception ex)
        {
            var routingKey = eventArgs.RoutingKey;
            var retryCount = GetRetryCount(eventArgs.BasicProperties);

            if (retryCount >= MaxRetries)
            {
                _logger.LogError(
                    ex, "Failed to process message on routing key '{RoutingKey}' (delivery tag {DeliveryTag}) after {RetryCount} retries. Sending to dead-letter queue.",
                    routingKey, eventArgs.DeliveryTag, retryCount);
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            var delay = RetryBackoff[retryCount];
            var nextRetryCount = retryCount + 1;

            _logger.LogWarning(
                ex, "Failed to process message on routing key '{RoutingKey}' (delivery tag {DeliveryTag}). Retry {NextRetryCount}/{MaxRetries} in {DelaySeconds}s.",
                routingKey, eventArgs.DeliveryTag, nextRetryCount, MaxRetries, delay.TotalSeconds);

            await Task.Delay(delay);

            var headers = eventArgs.BasicProperties.Headers is not null
                ? new Dictionary<string, object>(eventArgs.BasicProperties.Headers)
                : new Dictionary<string, object>();
            headers[RetryCountHeader] = nextRetryCount;

            var retryProperties = channel.CreateBasicProperties();
            retryProperties.Persistent = true;
            retryProperties.ContentType = eventArgs.BasicProperties.ContentType;
            retryProperties.Headers = headers;

            channel.BasicPublish(
                exchange: eventArgs.Exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: retryProperties,
                body: eventArgs.Body);

            channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }

        private static int GetRetryCount(IBasicProperties properties)
        {
            if (properties.Headers is null || !properties.Headers.TryGetValue(RetryCountHeader, out var value) || value is null)
                return 0;

            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes => int.Parse(System.Text.Encoding.UTF8.GetString(bytes)),
                _ => 0
            };
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
