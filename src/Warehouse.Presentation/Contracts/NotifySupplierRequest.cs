using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Api.Contracts
{
    public class NotifySupplierRequest
    {
        /// <summary>Optional. A status-appropriate message is generated when this is omitted.</summary>
        [MaxLength(1000)]
        public string? Message { get; set; }
    }
}
