using MediatR;
using Warehouse.Notifications.Domain.Repositories;

namespace Warehouse.Notifications.Application.Commands
{
    public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
    {
        private readonly INotificationRepository _notificationRepository;

        public MarkNotificationAsReadCommandHandler(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
        {
            var notification = await _notificationRepository.GetByIdAsync(request.Id, cancellationToken);

            if (notification is null)
            {
                return false;
            }

            notification.MarkAsRead();
            await _notificationRepository.UpdateAsync(notification, cancellationToken);

            return true;
        }
    }
}
