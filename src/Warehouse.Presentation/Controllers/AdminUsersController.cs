using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(IWebHostEnvironment environment, ILogger<AdminUsersController> logger)
    {
        _environment = environment;
        _logger = logger;
    }
    [HttpPost("{uid}/role")]
    public async Task<IActionResult> SetRole(string uid, [FromBody] SetUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(uid))
        {
            return BadRequest(new { message = "A user UID is required." });
        }

        if (string.IsNullOrWhiteSpace(request?.Role))
        {
            return BadRequest(new { message = "A role value is required." });
        }

        try
        {
            var claims = new Dictionary<string, object> { ["role"] = request.Role };
            await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(uid, claims, cancellationToken);
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogWarning(ex, "Failed to set role for Firebase user {Uid}", uid);
            return BadRequest(new { message = $"Unable to set role for user '{uid}'. Verify the UID is correct." });
        }

        _logger.LogInformation("Role {Role} set for Firebase user {Uid}", request.Role, uid);

        return Ok(new { uid, role = request.Role });
    }
}
