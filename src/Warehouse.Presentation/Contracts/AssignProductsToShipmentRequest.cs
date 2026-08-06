using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Api.Contracts
{
    public class AssignProductsToShipmentRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one product must be supplied.")]
        public List<ShipmentProductAssignmentRequest> Products { get; set; } = new();
    }

    public class ShipmentProductAssignmentRequest
    {
        [Required]
        public Guid ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Assigned quantity must be greater than zero.")]
        public int Quantity { get; set; }
    }
}
