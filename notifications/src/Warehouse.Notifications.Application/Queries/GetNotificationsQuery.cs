using MediatR;
using Warehouse.Notifications.Application.Notifications;
using Warehouse.Notifications.Domain.Entities;

namespace Warehouse.Notifications.Application.Queries
{
    public record GetNotificationsQuery(NotificationStatus? Status) : IRequest<IEnumerable<NotificationViewModel>>;
}
