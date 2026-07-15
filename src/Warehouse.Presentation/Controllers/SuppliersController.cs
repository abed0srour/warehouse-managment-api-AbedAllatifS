using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Warehouse.Application.Suppliers.Commands;
using Warehouse.Application.Suppliers.Queries;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/suppliers")]
    public class SuppliersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SuppliersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSuppliers()
        {
            var suppliers = await _mediator.Send(new GetAllSuppliersQuery());
            return Ok(suppliers);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetSupplierById([FromRoute] Guid id)
        {
            var supplier = await _mediator.Send(new GetSupplierByIdQuery(id));
            if (supplier == null)
            {
                return NotFound();
            }
            return Ok(supplier);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest request)
        {
            try
            {
                var supplierId = await _mediator.Send(new CreateSupplierCommand(request.Name, request.Country, request.ContactEmail, request.PhoneNumber));
                var createdSupplier = await _mediator.Send(new GetSupplierByIdQuery(supplierId));
                return CreatedAtAction(nameof(GetSupplierById), new { id = supplierId }, createdSupplier);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeactivateSupplier([FromRoute] Guid id)
        {
            var wasDeactivated = await _mediator.Send(new DeactivateSupplierCommand(id));
            if (!wasDeactivated)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}