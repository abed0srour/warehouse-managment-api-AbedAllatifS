namespace Warehouse.Application.Shipments;

using System;
using System.Collections.Generic;

public class ShipmentViewModel
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; }
    public string Status { get; set; }
    public AddressViewModel Destination { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SupplierNotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public int TotalUnits { get; set; }
    public List<ShipmentLineViewModel> Lines { get; set; }
}

public class ShipmentLineViewModel
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
}

public class AddressViewModel
{
    public string Line1 { get; set; }
    public string City { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
}

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
