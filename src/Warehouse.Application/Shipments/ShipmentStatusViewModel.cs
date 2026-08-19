namespace Warehouse.Application.Shipments;

using System;
using System.Collections.Generic;

/// <summary>
/// Tracking view of a shipment: where it is now plus how it got there. Deliberately narrower
/// than <see cref="ShipmentViewModel"/> -- a tracking caller does not need the line items.
/// </summary>
public record ShipmentStatusViewModel(
    Guid Id,
    string ReferenceNumber,
    string Status,
    string SupplierName,
    string? TrackingNumber,
    DateTime? ExpectedDeliveryDate,
    DateTime? DispatchedAt,
    DateTime? DeliveredAt,
    DateTime? SupplierNotifiedAt,
    bool IsClosed,
    IReadOnlyList<ShipmentStatusHistoryDto> History);

public record ShipmentStatusHistoryDto(
    string FromStatus,
    string ToStatus,
    string? Note,
    DateTime OccurredAt);
