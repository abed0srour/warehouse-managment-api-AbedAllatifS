using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Notifications.Api.Entities;
using Warehouse.Notifications.Api.Notifications;
using Warehouse.Notifications.Api.Repositories;

namespace Warehouse.Notifications.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IMapper _mapper;

        public NotificationsController(
            INotificationRepository notificationRepository,
            IMapper mapper)
        {
            _notificationRepository = notificationRepository;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationViewModel>>> GetNotifications(
            [FromQuery] NotificationStatus? status,
            CancellationToken cancellationToken)
        {
            var notifications = await _notificationRepository.GetAllAsync(cancellationToken);

            if (status is not null)
            {
                notifications = notifications.Where(n => n.Status == status);
            }

            return Ok(_mapper.Map<IEnumerable<NotificationViewModel>>(notifications));
        }

        [HttpPatch("{id}/read")]
        public async Task<ActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
        {
            var notification = await _notificationRepository.GetByIdAsync(id, cancellationToken);

            if (notification is null)
                return NotFound();

            notification.Status = NotificationStatus.Read;
            await _notificationRepository.UpdateAsync(notification, cancellationToken);

            return NoContent();
        }
    }
}