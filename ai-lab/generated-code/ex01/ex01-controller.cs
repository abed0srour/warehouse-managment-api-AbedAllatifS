using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Products.Queries;

namespace Warehouse.Presentation.Controllers;

public partial class ProductsController
{
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("expiring-soon")]
    public async Task<ActionResult<IEnumerable<ExpiringSoonProductDto>>> GetExpiringSoon(
        [FromQuery] int withinDays = 30,
        CancellationToken cancellationToken = default)
    {
        var products = await _mediator.Send(new GetExpiringSoonProductsQuery(withinDays), cancellationToken);
        return Ok(products);
    }
}
