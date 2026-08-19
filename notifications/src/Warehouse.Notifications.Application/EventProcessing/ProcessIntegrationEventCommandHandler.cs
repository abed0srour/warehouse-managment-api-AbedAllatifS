using MediatR;
using Warehouse.Notifications.Domain.Entities;
using Warehouse.Notifications.Domain.Preferences;
using Warehouse.Notifications.Domain.Repositories;

namespace Warehouse.Notifications.Application.EventProcessing
{
    public class ProcessIntegrationEventCommandHandler : IRequestHandler<ProcessIntegrationEventCommand, ProcessEventResult>
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly INotificationPreferenceProvider _preferenceProvider;

        public ProcessIntegrationEventCommandHandler(
            INotificationRepository notificationRepository,
            INotificationPreferenceProvider preferenceProvider)
        {
            _notificationRepository = notificationRepository;
            _preferenceProvider = preferenceProvider;
        }

        public async Task<ProcessEventResult> Handle(ProcessIntegrationEventCommand request, CancellationToken cancellationToken)
        {
            var eventId = request.Event.EventId.ToString();

            if (await _notificationRepository.ExistsByEventIdAsync(eventId, cancellationToken))
            {
                return ProcessEventResult.SkippedDuplicate;
            }

            var notificationType = EventTypeMapper.ToNotificationType(request.RoutingKey);
            var preference = _preferenceProvider.GetPreference(notificationType);

            if (preference is not null && !preference.Enabled)
            {
                return ProcessEventResult.SkippedByPreference;
            }

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                Type = notificationType,
                Title = EventTypeMapper.ToTitle(request.RoutingKey),
                Message = EventTypeMapper.ToMessage(request.RoutingKey, request.Event),
                Severity = preference?.Severity ?? request.Event.Severity,
                Status = NotificationStatus.Unread,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = request.Event.RelatedEntityId,
                RelatedEntityType = request.Event.RelatedEntityType,
                EventId = eventId
            };

            await _notificationRepository.AddAsync(notification, cancellationToken);

            return ProcessEventResult.Created;
        }
    }
}
