using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Warehouse.Presentation.Contracts
{
    /// <summary>Direction of a stock adjustment.</summary>
    // Serialised by name so the payload reads "Increase" rather than 0. The converter still
    // accepts the numeric form, so any existing caller keeps working.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AdjustmentType
    {
        Increase,
        Decrease
    }

    /// <summary>A request to receive stock into, or issue stock out of, a single product.</summary>
    public class CreateStockAdjustmentRequest : IValidatableObject
    {
        /// <summary>Identifier of the product whose stock level is changing.</summary>
        [Required(ErrorMessage = "Product ID is required.")]
        public Guid ProductId { get; set; }

        /// <summary>Whether the adjustment adds to or removes from the current level.</summary>
        [Required(ErrorMessage = "Adjustment type (Increase/Decrease) is required.")]
        public AdjustmentType Type { get; set; }

        /// <summary>Magnitude of the adjustment. Always positive; the direction comes from Type.</summary>
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public int Quantity { get; set; }

        /// <summary>Why the adjustment was made. Required when decreasing stock.</summary>
        [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
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
