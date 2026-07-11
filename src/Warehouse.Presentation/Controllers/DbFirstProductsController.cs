using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using Warehouse.Application.Products.Queries;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/db-first/products")]
public class DbFirstProductsController : ControllerBase
{
    private readonly ISender _mediator;

    public DbFirstProductsController(ISender mediator)
    {
        _mediator = mediator;
    }

    // 1. Get all products of a specific supplier
    [HttpGet("by-supplier")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySupplier([FromQuery] string supplierName, [FromQuery] string sortOrder = "desc")
    {
        var result = await _mediator.Send(new GetProductsBySupplierQuery(supplierName, sortOrder));
        return Ok(result);
    }

    // 2. Group products by expiry year
    [HttpGet("group-by-expiry-year")]
    [ProducesResponseType(typeof(IEnumerable<ExpiryYearGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GroupByExpiryYear()
    {
        var result = await _mediator.Send(new GetProductsGroupedByExpiryYearQuery());
        return Ok(result);
    }

    // 3. Group products by expiry year and supplier country[cite: 3]
    [HttpGet("group-by-expiry-and-country")]
    [ProducesResponseType(typeof(IEnumerable<ExpiryYearAndCountryGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GroupByExpiryAndCountry()
    {
        var result = await _mediator.Send(new GetProductsGroupedByExpiryAndCountryQuery());
        return Ok(result);
    }

    // 4. Get total number of products[cite: 3]
    [HttpGet("count")]
    [ProducesResponseType(typeof(Dictionary<string, int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCount()
    {
        var count = await _mediator.Send(new GetTotalProductCountQuery());
        return Ok(new { TotalProducts = count });
    }

    // 5. Server-side pagination[cite: 3]
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResponseDto<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1 || pageSize < 1) 
            return BadRequest("Page number and size must be greater than 0.");
            
        var result = await _mediator.Send(new GetPagedProductsQuery(pageNumber, pageSize));
        return Ok(result);
    }
}