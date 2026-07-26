using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Warehouse.Domain.Events;
using Warehouse.Notifications.Api.Entities;
using Warehouse.Notifications.Api.Repositories;

namespace Warehouse.Notifications.Api.Messaging
{
    public class NotificationEventConsumer : BackgroundService
    {
        private const string StockLowRoutingKey = "stock.low";
        private const string FileUploadedRoutingKey = "file.uploaded";
        private const string DeadLetterExchangeSuffix = ".dlx";
        private const string DeadLetterQueueSuffix = ".dlq";

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

            // Poison messages (bad JSON, unknown routing key) are nacked without requeue.
            // Routing them here instead of silently dropping them keeps the failure visible
            // in the RabbitMQ Management Console instead of vanishing.
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
                var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

                var eventId = @event.EventId.ToString();

                if (await repository.ExistsByEventIdAsync(eventId, CancellationToken.None))
                {
                    _logger.LogInformation(
                        "Event {EventId} on routing key '{RoutingKey}' was already processed; skipping.",
                        eventId, routingKey);
                    channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                    return;
                }

                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    Type = EventTypeMapper.ToNotificationType(routingKey),
                    Title = EventTypeMapper.ToTitle(routingKey),
                    Message = EventTypeMapper.ToMessage(routingKey, @event),
                    Severity = @event.Severity,
                    Status = NotificationStatus.Unread,
                    CreatedAt = DateTime.UtcNow,
                    RelatedEntityId = @event.RelatedEntityId,
                    RelatedEntityType = @event.RelatedEntityType,
                    EventId = eventId
                };

                await repository.AddAsync(notification, CancellationToken.None);

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
                // A concurrent redelivery inserted the same EventId first; the DB's unique
                // index (not this in-memory check) is the real idempotency guarantee.
                _logger.LogInformation(
                    "Unique constraint prevented a duplicate insert for routing key '{RoutingKey}' (delivery tag {DeliveryTag}).",
                    routingKey, eventArgs.DeliveryTag);
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Failed to process message on routing key '{RoutingKey}' (delivery tag {DeliveryTag}). Requeueing for retry.",
                    routingKey, eventArgs.DeliveryTag);
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
