using MediatR;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Notifications.Application.Commands;
using Warehouse.Notifications.Application.Notifications;
using Warehouse.Notifications.Application.Queries;
using Warehouse.Notifications.Domain.Entities;

namespace Warehouse.Notifications.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public NotificationsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationViewModel>>> GetNotifications(
            [FromQuery] NotificationStatus? status,
            CancellationToken cancellationToken)
        {
            var notifications = await _mediator.Send(new GetNotificationsQuery(status), cancellationToken);
            return Ok(notifications);
        }

        [HttpPatch("{id}/read")]
        public async Task<ActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
        {
            var wasMarked = await _mediator.Send(new MarkNotificationAsReadCommand(id), cancellationToken);

            if (!wasMarked)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
