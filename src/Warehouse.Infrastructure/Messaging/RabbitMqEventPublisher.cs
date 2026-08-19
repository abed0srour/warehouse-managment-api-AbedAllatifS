using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Warehouse.Domain.Events;

namespace Warehouse.Infrastructure.Messaging
{
    public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
    {
        private readonly RabbitMqOptions _options;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public RabbitMqEventPublisher(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;
        }

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null)
                return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_channel is not null)
                    return;

                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    UserName = _options.Username,
                    Password = _options.Password
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await _channel.ExchangeDeclareAsync(
                    exchange: _options.Exchange,
                    type: ExchangeType.Topic,
                    durable: true,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken cancellationToken)
            where TEvent : IntegrationEvent
        {
            await EnsureInitializedAsync(cancellationToken);

            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _channel!.BasicPublishAsync(
                exchange: _options.Exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel is not null)
                await _channel.CloseAsync();

            if (_connection is not null)
                await _connection.CloseAsync();
        }
    }
}