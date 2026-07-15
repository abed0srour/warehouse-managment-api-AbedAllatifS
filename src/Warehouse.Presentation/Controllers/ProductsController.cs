using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Commands;
using Warehouse.Application.Products.Queries;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/db-first/products")]
public class ProductsController : ControllerBase
{
    private readonly ISender _mediator;

    public ProductsController(ISender mediator)
    {
        _mediator = mediator;
    }

    // 1. Get all products of a specific supplier
    [HttpGet("by-supplier")]
    [ProducesResponseType(typeof(IEnumerable<ProductViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySupplier([FromQuery] string supplierName, [FromQuery] string sortOrder = "desc", CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetProductsBySupplierQuery(supplierName, sortOrder), cancellationToken);
        return Ok(result);
    }

    // 2. Group products by expiry year
    [HttpGet("group-by-expiry-year")]
    [ProducesResponseType(typeof(IEnumerable<ExpiryYearGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GroupByExpiryYear(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetProductsGroupedByExpiryYearQuery(), cancellationToken);
        return Ok(result);
    }

    // 3. Group products by expiry year and supplier country
    [HttpGet("group-by-expiry-and-country")]
    [ProducesResponseType(typeof(IEnumerable<ExpiryYearAndCountryGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GroupByExpiryAndCountry(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetProductsGroupedByExpiryAndCountryQuery(), cancellationToken);
        return Ok(result);
    }

    // 4. Get total number of products
    [HttpGet("count")]
    [ProducesResponseType(typeof(Dictionary<string, int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCount(CancellationToken cancellationToken)
    {
        var count = await _mediator.Send(new GetTotalProductCountQuery(), cancellationToken);
        return Ok(new { TotalProducts = count });
    }

    // 5. Server-side pagination
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResponseDto<ProductViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageSize < 1)
            return BadRequest("Page number and size must be greater than 0.");

        var result = await _mediator.Send(new GetPagedProductsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    // 6. Get product by id
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        if (product is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product not found",
                Detail = $"Product with ID {id} was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(product);
    }

    // 7. Create product
    [HttpPost]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CreateProductCommand(request.Name, request.SKU, request.Description, request.Price, request.QuantityInStock, request.ExpiryDate),
            cancellationToken);

        return FromResult(result, product => CreatedAtAction(nameof(GetById), new { id = product.Id }, product));
    }

    // 8. Update quantity
    [HttpPost("{id:guid}/quantity")]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuantity([FromRoute] Guid id, [FromBody] UpdateProductQuantityRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateProductQuantityCommand(id, request.QuantityInStock), cancellationToken);
        return FromResult(result, Ok);
    }

    // 9. Update price
    [HttpPost("{id:guid}/price")]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePrice([FromRoute] Guid id, [FromBody] UpdateProductPriceRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateProductPriceCommand(id, request.Price), cancellationToken);
        return FromResult(result, Ok);
    }

    // 10. Assign supplier
    [HttpPost("{id:guid}/assign-supplier/{supplierId:guid}")]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignSupplier([FromRoute] Guid id, [FromRoute] Guid supplierId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AssignSupplierCommand(id, supplierId), cancellationToken);
        return FromResult(result, Ok);
    }

    // 11. Archive (soft delete)
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveProduct([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ArchiveProductCommand(id), cancellationToken);
        return FromResult(result, Ok);
    }

    private IActionResult FromResult<T>(Result<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
        {
            return onSuccess(result.Value!);
        }

        var error = result.Error!;
        var problemDetails = new ProblemDetails
        {
            Title = error.Type.ToString(),
            Detail = error.Message,
            Status = error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            }
        };

        return StatusCode(problemDetails.Status!.Value, problemDetails);
    }
}
