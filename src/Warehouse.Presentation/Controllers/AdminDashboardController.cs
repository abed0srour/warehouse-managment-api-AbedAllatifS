using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Domain;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly INotificationServiceClient _notificationServiceClient;

    public AdminDashboardController(INotificationServiceClient notificationServiceClient)
    {
        _notificationServiceClient = notificationServiceClient;
    }

    // GET /api/admin/dashboard/unread-notifications
    // Read-only call into the Notification Service. If it's unreachable, times out, or
    // errors, this returns a safe "unavailable" response instead of a 500 — it must not
    // affect any other warehouse endpoint.
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("unread-notifications")]
    public async Task<IActionResult> GetUnreadNotificationCount(CancellationToken cancellationToken)
    {
        var count = await _notificationServiceClient.GetUnreadNotificationCountAsync(cancellationToken);

        if (count is null)
        {
            return Ok(new { available = false, unreadCount = (int?)null });
        }

        return Ok(new { available = true, unreadCount = count });
    }
}
