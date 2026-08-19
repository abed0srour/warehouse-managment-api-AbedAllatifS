using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Warehouse.Application.Common;
using Warehouse.Application.Shipments;
using Warehouse.Application.Shipments.Commands;
using Warehouse.Application.Shipments.Queries;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShipmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ShipmentsController> _logger;

    public ShipmentsController(IMediator mediator, ILogger<ShipmentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    // GET /api/shipments/{id}
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShipmentViewModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var shipment = await _mediator.Send(new GetShipmentByIdQuery(id), cancellationToken);
        if (shipment == null)
        {
            _logger.LogWarning("Shipment {ShipmentId} was not found", id);
            return NotFound(new { message = $"Shipment with ID {id} was not found." });
        }

        return Ok(shipment);
    }

    // GET /api/shipments/{id}/status
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<ShipmentStatusViewModel>> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var status = await _mediator.Send(new GetShipmentStatusQuery(id), cancellationToken);
        if (status == null)
        {
            return NotFound(new { message = $"Shipment with ID {id} was not found." });
        }

        return Ok(status);
    }

    // POST /api/shipments
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<ActionResult<ShipmentViewModel>> Create(
        [FromBody] CreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateShipmentCommand(
            request.SupplierId,
            request.DestinationLine1,
            request.DestinationCity,
            request.DestinationPostalCode,
            request.DestinationCountry,
            request.ExpectedDeliveryDate,
            request.ReferenceNumber), cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        _logger.LogInformation(
            "Shipment {ShipmentId} created with reference {ReferenceNumber}",
            result.Value!.Id, result.Value.ReferenceNumber);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    // POST /api/shipments/{id}/products
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/products")]
    public async Task<ActionResult<ShipmentViewModel>> AssignProducts(
        Guid id,
        [FromBody] AssignProductsToShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var assignments = request.Products
            .Select(p => new ShipmentProductAssignment(p.ProductId, p.Quantity))
            .ToList();

        var result = await _mediator.Send(new AssignProductsToShipmentCommand(id, assignments), cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        _logger.LogInformation("{LineCount} product line(s) assigned to shipment {ShipmentId}", assignments.Count, id);

        return Ok(result.Value);
    }

    // POST /api/shipments/{id}/delivery-state
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/delivery-state")]
    public async Task<ActionResult<ShipmentViewModel>> UpdateDeliveryState(
        Guid id,
        [FromBody] UpdateDeliveryStateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateDeliveryStateCommand(id, request.Status, request.TrackingNumber, request.Note),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        _logger.LogInformation("Shipment {ShipmentId} moved to delivery state {Status}", id, result.Value!.Status);

        return Ok(result.Value);
    }

    // POST /api/shipments/{id}/notify-supplier
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/notify-supplier")]
    public async Task<ActionResult<ShipmentViewModel>> NotifySupplier(
        Guid id,
        [FromBody] NotifySupplierRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new NotifySupplierCommand(id, request?.Message), cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        _logger.LogInformation("Supplier notified about shipment {ShipmentId}", id);

        return Ok(result.Value);
    }

    // Same Result -> status code mapping the products and suppliers controllers use,
    // pulled into one place because five endpoints share it.
    private ActionResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => NotFound(new { message = error.Message }),
        ErrorType.Conflict => Conflict(new { message = error.Message }),
        _ => BadRequest(new { message = error.Message })
    };
}
