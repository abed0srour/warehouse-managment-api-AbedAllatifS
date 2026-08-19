using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Products.Queries;

namespace Warehouse.Presentation.Controllers;

public partial class ProductsController
{
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("out-of-stock")]
    [ProducesResponseType(typeof(IEnumerable<OutOfStockProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OutOfStockProductDto>>> GetOutOfStock(CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetOutOfStockProductsQuery(), cancellationToken);
        return Ok(products);
    }
}
