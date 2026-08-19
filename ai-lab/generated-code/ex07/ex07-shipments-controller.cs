using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Application.Shipments;
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

    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("{id:guid}")]
    public Task<ActionResult<ShipmentViewModel>> GetById(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("{id:guid}/status")]
    public Task<ActionResult<ShipmentStatusViewModel>> GetStatus(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public Task<ActionResult<ShipmentViewModel>> Create(
        [FromBody] CreateShipmentRequest request,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/products")]
    public Task<ActionResult<ShipmentViewModel>> AssignProducts(
        Guid id,
        [FromBody] AssignProductsToShipmentRequest request,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/delivery-state")]
    public Task<ActionResult<ShipmentViewModel>> UpdateDeliveryState(
        Guid id,
        [FromBody] UpdateDeliveryStateRequest request,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/notify-supplier")]
    public Task<ActionResult<ShipmentViewModel>> NotifySupplier(
        Guid id,
        [FromBody] NotifySupplierRequest? request,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    private ActionResult MapError(Error error) => throw new NotImplementedException();
}
