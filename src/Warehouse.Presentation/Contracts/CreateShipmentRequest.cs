using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Api.Contracts
{
    public class CreateShipmentRequest
    {
        [Required]
        public Guid SupplierId { get; set; }

        [Required]
        public string DestinationLine1 { get; set; } = string.Empty;

        [Required]
        public string DestinationCity { get; set; } = string.Empty;

        public string DestinationPostalCode { get; set; } = string.Empty;

        [Required]
        public string DestinationCountry { get; set; } = string.Empty;

        public DateTime? ExpectedDeliveryDate { get; set; }

        /// <summary>Optional. The API assigns an "SHP-yyyyMMdd-XXXXXXXX" reference when this is omitted.</summary>
        [MaxLength(50)]
        public string? ReferenceNumber { get; set; }
    }
}
