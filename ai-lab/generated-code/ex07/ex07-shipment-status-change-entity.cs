namespace Warehouse.Domain;

using System;

public class ShipmentStatusChange
{
    public Guid Id { get; private set; }
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
        DateTime occurredAt) => throw new NotImplementedException();

    public static ShipmentStatusChange Reconstruct(
        Guid id,
        Guid shipmentId,
        ShipmentStatus fromStatus,
        ShipmentStatus toStatus,
        string? note,
        DateTime occurredAt) => throw new NotImplementedException();
}
