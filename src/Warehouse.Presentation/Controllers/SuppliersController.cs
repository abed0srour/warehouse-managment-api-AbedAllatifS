using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common;
using Warehouse.Application.Suppliers;
using Warehouse.Application.Suppliers.Commands;
using Warehouse.Application.Suppliers.Queries;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SuppliersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/suppliers
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SupplierViewModel>>> GetAll(CancellationToken cancellationToken)
    {
        var suppliers = await _mediator.Send(new GetAllSuppliersQuery(), cancellationToken);
        return Ok(suppliers);
    }

    // GET /api/suppliers/{id}
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierViewModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _mediator.Send(new GetSupplierByIdQuery(id), cancellationToken);
        if (supplier == null)
        {
            return NotFound(new { message = $"Supplier with ID {id} was not found." });
        }

        return Ok(supplier);
    }

    // POST /api/suppliers
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<ActionResult<SupplierViewModel>> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateSupplierCommand(
            request.Name,
            request.Country,
            request.ContactEmail,
            request.PhoneNumber), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Error!.Message });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // DELETE /api/suppliers/{id} - deactivate, not remove
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeactivateSupplierCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error!.Type switch
            {
                ErrorType.NotFound => NotFound(new { message = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { message = result.Error.Message }),
                _ => BadRequest(new { message = result.Error.Message })
            };
        }

        return NoContent();
    }
}
