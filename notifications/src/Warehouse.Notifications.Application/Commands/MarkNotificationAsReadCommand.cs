using MediatR;

namespace Warehouse.Notifications.Application.Commands
{
    public record MarkNotificationAsReadCommand(Guid Id) : IRequest<bool>;
}
