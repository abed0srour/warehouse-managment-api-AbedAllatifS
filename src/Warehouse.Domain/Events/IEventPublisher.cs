namespace Warehouse.Domain.Events
{
    public interface IEventPublisher
    {
        Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken cancellationToken)
            where TEvent : IntegrationEvent;
    }
}