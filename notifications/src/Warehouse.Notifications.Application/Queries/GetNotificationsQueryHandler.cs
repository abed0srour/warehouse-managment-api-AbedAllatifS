using AutoMapper;
using MediatR;
using Warehouse.Notifications.Application.Notifications;
using Warehouse.Notifications.Domain.Repositories;

namespace Warehouse.Notifications.Application.Queries
{
    public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, IEnumerable<NotificationViewModel>>
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IMapper _mapper;

        public GetNotificationsQueryHandler(INotificationRepository notificationRepository, IMapper mapper)
        {
            _notificationRepository = notificationRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<NotificationViewModel>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
        {
            var notifications = await _notificationRepository.GetAllAsync(cancellationToken);

            if (request.Status is not null)
            {
                notifications = notifications.Where(n => n.Status == request.Status);
            }

            return _mapper.Map<IEnumerable<NotificationViewModel>>(notifications);
        }
    }
}
