namespace Warehouse.Domain;

using System;

/// <summary>
/// An append-only audit entry for one transition of a shipment's status. This is what makes
/// "track shipment status" answerable as history rather than only as a current value.
/// </summary>
public class ShipmentStatusChange
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShipmentId { get; private set; }
    public ShipmentStatus FromStatus { get; private set; }
    public ShipmentStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private ShipmentStatusChange()
    {
    }

    internal static ShipmentStatusChange Record(
        Guid shipmentId,
        ShipmentStatus from,
        ShipmentStatus to,
        string? note,
        DateTime occurredAt) => new()
        {
            ShipmentId = shipmentId,
            FromStatus = from,
            ToStatus = to,
            Note = note,
            OccurredAt = occurredAt
        };

    // Rehydrates an entry from persisted state.
    public static ShipmentStatusChange Reconstruct(
        Guid id,
        Guid shipmentId,
        ShipmentStatus fromStatus,
        ShipmentStatus toStatus,
        string? note,
        DateTime occurredAt) => new()
        {
            Id = id,
            ShipmentId = shipmentId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = note,
            OccurredAt = occurredAt
        };
}
