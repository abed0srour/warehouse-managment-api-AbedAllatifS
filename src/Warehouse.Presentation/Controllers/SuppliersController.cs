using MediatR;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Suppliers.Commands;
using Warehouse.Application.Suppliers.Queries;
using Warehouse.Domain;
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
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetAll(CancellationToken cancellationToken)
    {
        var suppliers = await _mediator.Send(new GetAllSuppliersQuery(), cancellationToken);
        return Ok(suppliers);
    }

    // GET /api/suppliers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Supplier>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _mediator.Send(new GetSupplierByIdQuery(id), cancellationToken);
        if (supplier == null)
        {
            return NotFound(new { message = $"Supplier with ID {id} was not found." });
        }

        return Ok(supplier);
    }

    // POST /api/suppliers
    [HttpPost]
    public async Task<ActionResult<Supplier>> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _mediator.Send(new CreateSupplierCommand(
            request.Name,
            request.Country,
            request.ContactEmail,
            request.PhoneNumber), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
    }

    // DELETE /api/suppliers/{id} - deactivate, not remove
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var deactivated = await _mediator.Send(new DeactivateSupplierCommand(id), cancellationToken);
        if (!deactivated)
        {
            return NotFound(new { message = $"Supplier with ID {id} was not found." });
        }

        return NoContent();
    }
}
