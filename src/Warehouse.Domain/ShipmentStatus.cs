namespace Warehouse.Domain;

/// <summary>
/// The single lifecycle a shipment moves through. "Shipment status" and "delivery state"
/// are the same concept here -- one state machine, not two overlapping ones.
/// </summary>
public enum ShipmentStatus
{
    Draft,
    Dispatched,
    InTransit,
    Delivered,
    Cancelled
}
