using System;
using System.ComponentModel.DataAnnotations;
namespace WarehouseManagement.Api.Contracts
{
    public class UpdateProductQuantityRequest
    {
        [Range(0, int.MaxValue)]
        public int QuantityInStock { get; set; }
    }
}