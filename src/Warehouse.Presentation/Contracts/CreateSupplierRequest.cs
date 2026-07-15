using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Api.Contracts
{
    public class CreateSupplierRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Country { get; set; } = string.Empty;
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string ContactEmail { get; set; } = string.Empty;
        [Required]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}