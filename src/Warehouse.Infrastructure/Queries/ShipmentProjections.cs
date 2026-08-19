using System.Linq.Expressions;
using Warehouse.Application.Shipments;
using Warehouse.Infrastructure.Data.EfModels;
// Not a blanket `using Warehouse.Domain`: the domain and EF layers both define a Shipment,
// and this file projects from the EF one.
using DomainShipmentStatus = Warehouse.Domain.ShipmentStatus;

namespace Warehouse.Infrastructure.Queries;

/// <summary>
/// Shared EF projections for the shipment read side, kept as <see cref="Expression"/> so they
/// compose into a query and are translated by Npgsql rather than evaluated client-side --
/// the same reason <see cref="ProductProjections"/> exists.
/// </summary>
internal static class ShipmentProjections
{
    public static readonly Expression<Func<Shipment, ShipmentViewModel>> ToViewModel =
        s => new ShipmentViewModel
        {
            Id = s.Id,
            ReferenceNumber = s.ReferenceNumber,
            SupplierId = s.SupplierId,
            SupplierName = s.SupplierName,
            Status = s.Status,
            Destination = new AddressViewModel
            {
                Line1 = s.DestinationLine1,
                City = s.DestinationCity,
                PostalCode = s.DestinationPostalCode,
                Country = s.DestinationCountry
            },
            ExpectedDeliveryDate = s.ExpectedDeliveryDate,
            TrackingNumber = s.TrackingNumber,
            DispatchedAt = s.DispatchedAt,
            DeliveredAt = s.DeliveredAt,
            SupplierNotifiedAt = s.SupplierNotifiedAt,
            CreatedAt = s.CreatedAt,
            LastUpdatedAt = s.LastUpdatedAt,
            TotalUnits = s.Lines.Sum(l => (int?)l.Quantity) ?? 0,
            Lines = s.Lines
                .OrderBy(l => l.ProductName)
                .Select(l => new ShipmentLineViewModel
                {
                    Id = l.Id,
                    ProductId = l.ProductId,
                    ProductName = l.ProductName,
                    Sku = l.Sku,
                    Quantity = l.Quantity
                })
                .ToList()
        };

    public static readonly Expression<Func<Shipment, ShipmentStatusViewModel>> ToStatusViewModel =
        s => new ShipmentStatusViewModel(
            s.Id,
            s.ReferenceNumber,
            s.Status,
            s.SupplierName,
            s.TrackingNumber,
            s.ExpectedDeliveryDate,
            s.DispatchedAt,
            s.DeliveredAt,
            s.SupplierNotifiedAt,
            s.Status == nameof(DomainShipmentStatus.Delivered) || s.Status == nameof(DomainShipmentStatus.Cancelled),
            s.StatusHistory
                .OrderBy(h => h.OccurredAt)
                .Select(h => new ShipmentStatusHistoryDto(h.FromStatus, h.ToStatus, h.Note, h.OccurredAt))
                .ToList());
}
