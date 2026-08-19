namespace Warehouse.Application.Shipments
{
    using System;
    using System.Collections.Generic;

    public class ShipmentViewModel
    {
        public Guid Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        // Serialised as the enum name rather than its ordinal: the API stays readable and
        // reordering ShipmentStatus cannot change the meaning of a stored or published value.
        public string Status { get; set; } = string.Empty;

        public AddressViewModel Destination { get; set; } = new();
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? SupplierNotifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public int TotalUnits { get; set; }
        public List<ShipmentLineViewModel> Lines { get; set; } = new();
    }

    public class ShipmentLineViewModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class AddressViewModel
    {
        public string Line1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}
