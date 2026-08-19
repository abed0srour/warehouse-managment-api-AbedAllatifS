namespace Warehouse.Domain;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Aggregate root for outbound shipments. Owns its lines and its status history: every
/// mutation goes through a method here so the state machine and the "only a draft can be
/// edited" rule cannot be bypassed by a caller setting properties directly.
/// </summary>
public class Shipment
{
    // Which transitions are legal. Delivered and Cancelled are terminal, so they are absent.
    private static readonly IReadOnlyDictionary<ShipmentStatus, ShipmentStatus[]> AllowedTransitions =
        new Dictionary<ShipmentStatus, ShipmentStatus[]>
        {
            [ShipmentStatus.Draft] = new[] { ShipmentStatus.Dispatched, ShipmentStatus.Cancelled },
            [ShipmentStatus.Dispatched] = new[] { ShipmentStatus.InTransit, ShipmentStatus.Delivered, ShipmentStatus.Cancelled },
            [ShipmentStatus.InTransit] = new[] { ShipmentStatus.Delivered, ShipmentStatus.Cancelled }
        };

    private readonly List<ShipmentLine> _lines = new();
    private readonly List<ShipmentStatusChange> _statusHistory = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ReferenceNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }

    // Denormalised like Product.SupplierName, so a shipment still reads correctly
    // if the supplier row is later renamed.
    public string SupplierName { get; private set; } = string.Empty;

    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Draft;
    public Address Destination { get; private set; } = null!;
    public DateTime? ExpectedDeliveryDate { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? SupplierNotifiedAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = Now();
    public DateTime? LastUpdatedAt { get; private set; }

    public IReadOnlyCollection<ShipmentLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<ShipmentStatusChange> StatusHistory => _statusHistory.AsReadOnly();

    public int TotalUnits => _lines.Sum(l => l.Quantity);
    public bool IsClosed => Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled;

    private Shipment()
    {
    }

    public static Shipment Create(
        string referenceNumber,
        Supplier supplier,
        Address destination,
        DateTime? expectedDeliveryDate)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(destination);

        if (string.IsNullOrWhiteSpace(referenceNumber))
            throw new ArgumentException("Shipment reference number is required.");

        if (!supplier.IsActive)
            throw new InvalidOperationException("Shipments cannot be created for an inactive supplier.");

        var shipment = new Shipment
        {
            ReferenceNumber = referenceNumber.Trim(),
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            Destination = destination,
            ExpectedDeliveryDate = AsUnspecified(expectedDeliveryDate),
            Status = ShipmentStatus.Draft
        };

        if (shipment.ExpectedDeliveryDate.HasValue && shipment.ExpectedDeliveryDate.Value < shipment.CreatedAt.Date)
            throw new ArgumentException("Expected delivery date cannot be in the past.");

        return shipment;
    }

    // Rehydrates a Shipment from persisted state without re-running creation invariants,
    // mirroring Product.Reconstruct.
    public static Shipment Reconstruct(
        Guid id,
        string referenceNumber,
        Guid supplierId,
        string supplierName,
        ShipmentStatus status,
        Address destination,
        DateTime? expectedDeliveryDate,
        string? trackingNumber,
        DateTime? dispatchedAt,
        DateTime? deliveredAt,
        DateTime? supplierNotifiedAt,
        DateTime createdAt,
        DateTime? lastUpdatedAt,
        IEnumerable<ShipmentLine> lines,
        IEnumerable<ShipmentStatusChange> statusHistory)
    {
        var shipment = new Shipment
        {
            Id = id,
            ReferenceNumber = referenceNumber,
            SupplierId = supplierId,
            SupplierName = supplierName,
            Status = status,
            Destination = destination,
            ExpectedDeliveryDate = expectedDeliveryDate,
            TrackingNumber = trackingNumber,
            DispatchedAt = dispatchedAt,
            DeliveredAt = deliveredAt,
            SupplierNotifiedAt = supplierNotifiedAt,
            CreatedAt = createdAt,
            LastUpdatedAt = lastUpdatedAt
        };

        shipment._lines.AddRange(lines);
        shipment._statusHistory.AddRange(statusHistory.OrderBy(h => h.OccurredAt));

        return shipment;
    }

    /// <summary>
    /// Adds a product to the shipment, or tops up the quantity if that product is already on it.
    /// Only a draft can be changed -- once dispatched, the contents are what left the warehouse.
    /// </summary>
    public void AssignProduct(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (Status != ShipmentStatus.Draft)
            throw new InvalidOperationException("Products can only be assigned while the shipment is a draft.");

        if (product.IsArchived)
            throw new InvalidOperationException("Archived products cannot be assigned to a shipment.");

        if (quantity <= 0)
            throw new ArgumentException("Assigned quantity must be greater than zero.");

        if (quantity > product.QuantityInStock)
            throw new InvalidOperationException(
                $"Cannot assign {quantity} of '{product.Name}': only {product.QuantityInStock} in stock.");

        var existing = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing == null)
        {
            _lines.Add(ShipmentLine.For(Id, product, quantity));
        }
        else
        {
            existing.IncreaseQuantity(quantity);
        }

        Touch();
    }

    public void RemoveProduct(Guid productId)
    {
        if (Status != ShipmentStatus.Draft)
            throw new InvalidOperationException("Products can only be removed while the shipment is a draft.");

        var line = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (line == null)
            return;

        _lines.Remove(line);
        Touch();
    }

    /// <summary>
    /// Moves the shipment to its next state, recording the transition in the history.
    /// </summary>
    public void UpdateDeliveryState(ShipmentStatus newStatus, string? trackingNumber = null, string? note = null)
    {
        if (newStatus == Status)
            throw new InvalidOperationException($"Shipment is already in state '{Status}'.");

        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOperationException($"Cannot move a shipment from '{Status}' to '{newStatus}'.");

        if (newStatus == ShipmentStatus.Dispatched && _lines.Count == 0)
            throw new InvalidOperationException("A shipment cannot be dispatched with no products assigned.");

        var occurredAt = Now();
        var previous = Status;

        Status = newStatus;

        if (!string.IsNullOrWhiteSpace(trackingNumber))
            TrackingNumber = trackingNumber.Trim();

        if (newStatus == ShipmentStatus.Dispatched)
            DispatchedAt = occurredAt;

        if (newStatus == ShipmentStatus.Delivered)
            DeliveredAt = occurredAt;

        _statusHistory.Add(ShipmentStatusChange.Record(Id, previous, newStatus, note, occurredAt));
        LastUpdatedAt = occurredAt;
    }

    /// <summary>
    /// Stamps the shipment with the moment its supplier was last told about it. Called after
    /// the notification has actually been handed off, so a failed send leaves no false record.
    /// </summary>
    public void RecordSupplierNotification(DateTime sentAt)
    {
        SupplierNotifiedAt = DateTime.SpecifyKind(sentAt, DateTimeKind.Unspecified);
        LastUpdatedAt = Now();
    }

    private void Touch() => LastUpdatedAt = Now();

    private static DateTime Now() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private static DateTime? AsUnspecified(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified) : null;
}
