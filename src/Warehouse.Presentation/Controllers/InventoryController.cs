using MediatR;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Inventory.Queries;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _mediator.Send(new GetInventoryDashboardQuery(), cancellationToken);
        return Ok(dashboard);
    }
}
