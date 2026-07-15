using System;
using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Api.Contracts
{
    public class UpdateProductPriceRequest

    {
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }
    }
}