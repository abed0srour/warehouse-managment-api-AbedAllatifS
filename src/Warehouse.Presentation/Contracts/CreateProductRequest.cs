using System;
using System.ComponentModel.DataAnnotations;


namespace WarehouseManagement.Api.Contracts
{
    public class CreateProductRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string SKU { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "Quantity in stock cannot be negative.")]
        public int QuantityInStock { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
    }
}