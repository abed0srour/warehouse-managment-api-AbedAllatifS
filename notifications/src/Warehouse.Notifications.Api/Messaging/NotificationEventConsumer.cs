using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Warehouse.Notifications.Api.Messaging
{
    public class NotificationEventConsumer : BackgroundService
    {
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
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true,
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = factory.CreateConnection();
                    using var channel = connection.CreateModel();

                    channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += OnMessageReceivedAsync;

                    channel.BasicConsume(_options.QueueName, autoAck: true, consumer);

                    _logger.LogInformation(
                        "NotificationEventConsumer connected to RabbitMQ at {HostName}:{Port}, listening on queue '{QueueName}'.",
                        _options.HostName, _options.Port, _options.QueueName);

                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "NotificationEventConsumer could not reach RabbitMQ at {HostName}:{Port}. Retrying in {RetryDelay}.",
                        _options.HostName, _options.Port, RetryDelay);

                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }

        private Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
        {
            using var scope = _scopeFactory.CreateScope();
            _logger.LogInformation("Received notification event on '{RoutingKey}' (handling not implemented yet).", eventArgs.RoutingKey);
            return Task.CompletedTask;
        }
    }
}
