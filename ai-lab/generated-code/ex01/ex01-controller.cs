// This method is an addition to the existing src/Warehouse.Presentation/Controllers/ProductsController.cs.
// Shown here wrapped in its own partial class declaration so this file is independently valid/compilable;
// in the real codebase it's just one more [HttpGet] action alongside the others already in that controller.

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Products.Queries;

namespace Warehouse.Presentation.Controllers;

public partial class ProductsController
{
    // GET /api/products/expiring-soon?withinDays=30
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
