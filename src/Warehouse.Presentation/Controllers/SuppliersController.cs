using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
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
        public async Task<IActionResult> GetAllSuppliers(CancellationToken cancellationToken)
        {
            var suppliers = await _mediator.Send(new GetAllSuppliersQuery(), cancellationToken);
            return Ok(suppliers);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetSupplierById([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var supplier = await _mediator.Send(new GetSupplierByIdQuery(id), cancellationToken);
            if (supplier == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Supplier not found",
                    Detail = $"Supplier with ID {id} was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }
            return Ok(supplier);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateSupplierCommand(request.Name, request.Country, request.ContactEmail, request.PhoneNumber),
                cancellationToken);

            if (!result.IsSuccess)
            {
                return MapError(result.Error!);
            }

            return CreatedAtAction(nameof(GetSupplierById), new { id = result.Value!.Id }, result.Value);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeactivateSupplier([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new DeactivateSupplierCommand(id), cancellationToken);
            if (!result.IsSuccess)
            {
                return MapError(result.Error!);
            }

            return NoContent();
        }

        private IActionResult MapError(Error error)
        {
            var status = error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };

            return StatusCode(status, new ProblemDetails
            {
                Title = error.Type.ToString(),
                Detail = error.Message,
                Status = status
            });
        }
    }
}
