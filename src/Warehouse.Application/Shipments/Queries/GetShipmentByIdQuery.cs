using MediatR;

namespace Warehouse.Application.Shipments.Queries;

/// <summary>
/// Full shipment, lines included. Handled in Warehouse.Infrastructure against the DbContext
/// so the whole graph is projected in one query instead of being rehydrated through the
/// repository and mapped afterwards.
/// </summary>
public record GetShipmentByIdQuery(Guid Id) : IRequest<ShipmentViewModel?>;
