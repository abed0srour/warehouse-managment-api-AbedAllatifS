using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Warehouse.Presentation.Contracts
{
    public enum AdjustmentType
    {
        Increase,
        Decrease
    }

    public class CreateStockAdjustmentRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Product ID is required.")]
        public Guid ProductId { get; set; }

        [Required(ErrorMessage = "Adjustment type (Increase/Decrease) is required.")]
        public AdjustmentType Type { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public int Quantity { get; set; }

        public string? Reason { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Type == AdjustmentType.Decrease && string.IsNullOrWhiteSpace(Reason))
            {
                yield return new ValidationResult(
                    "A reason is required when decreasing stock.",
                    new[] { nameof(Reason) }
                );
            }
        }
    }
}