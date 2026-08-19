namespace WarehouseManagement.Api.Contracts;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class CreateShipmentRequest
{
    [Required]
    public Guid SupplierId { get; set; }

    [Required]
    public string DestinationLine1 { get; set; }

    [Required]
    public string DestinationCity { get; set; }

    public string DestinationPostalCode { get; set; }

    [Required]
    public string DestinationCountry { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }
}

public class AssignProductsToShipmentRequest
{
    [Required]
    [MinLength(1)]
    public List<ShipmentProductAssignmentRequest> Products { get; set; }
}

public class ShipmentProductAssignmentRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class UpdateDeliveryStateRequest
{
    [Required]
    public string Status { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class NotifySupplierRequest
{
    [MaxLength(1000)]
    public string? Message { get; set; }
}
