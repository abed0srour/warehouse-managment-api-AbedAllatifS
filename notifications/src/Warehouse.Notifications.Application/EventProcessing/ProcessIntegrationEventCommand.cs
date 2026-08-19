using MediatR;
using Warehouse.Domain.Events;

namespace Warehouse.Notifications.Application.EventProcessing
{
    public record ProcessIntegrationEventCommand(IntegrationEvent Event, string RoutingKey) : IRequest<ProcessEventResult>;
}
