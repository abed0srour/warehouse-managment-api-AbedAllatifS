using MediatR;

namespace Warehouse.Application.Shipments.Queries;

/// <summary>
/// Tracking view: current state plus the ordered transition history. Not cached -- a shipment's
/// state is exactly the field that changes, so a stale five-minute read (as on
/// <c>GetProductByIdQuery</c>) would be worse than no cache at all.
/// </summary>
public record GetShipmentStatusQuery(Guid Id) : IRequest<ShipmentStatusViewModel?>;
