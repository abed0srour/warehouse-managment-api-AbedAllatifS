using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Api.Contracts
{
    public class UpdateDeliveryStateRequest
    {
        /// <summary>One of: Dispatched, InTransit, Delivered, Cancelled.</summary>
        [Required]
        public string Status { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? TrackingNumber { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }
}
