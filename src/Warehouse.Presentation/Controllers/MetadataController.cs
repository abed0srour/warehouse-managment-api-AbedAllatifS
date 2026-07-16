using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Exceptions;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/metadata")]
public class MetadataController : ControllerBase
{

    private static readonly Dictionary<string, Type> KnownDtos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CreateProductRequest"] = typeof(CreateProductRequest),
        ["UpdateQuantityRequest"] = typeof(UpdateQuantityRequest),
        ["UpdatePriceRequest"] = typeof(UpdatePriceRequest),
        ["CreateStockAdjustmentRequest"] = typeof(Warehouse.Presentation.Contracts.CreateStockAdjustmentRequest),
    };

    [HttpGet("validation/{dtoName}")]
    public IActionResult GetValidationMetadata(string dtoName)
    {
        if (!KnownDtos.TryGetValue(dtoName, out var type))
        {
            throw new NotFoundException($"No DTO named '{dtoName}' is registered for metadata inspection.");
        }

        var properties = type.GetProperties();

        var metadata = properties.Select(prop =>
        {
            var validationAttributes = prop
                .GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                .Cast<ValidationAttribute>();

            var rules = validationAttributes.Select(DescribeAttribute).ToList();

            return new
            {
                PropertyName = prop.Name,
                PropertyType = prop.PropertyType.Name,
                Required = rules.Any(r => r.StartsWith("Required")),
                Rules = rules
            };
        }).ToList();

        return Ok(new
        {
            DtoName = type.Name,
            Properties = metadata
        });
    }

    private static string DescribeAttribute(ValidationAttribute attribute)
    {
        return attribute switch
        {
            RequiredAttribute => "Required",
            RangeAttribute range => $"Range({range.Minimum}, {range.Maximum})",
            StringLengthAttribute length => $"StringLength(max: {length.MaximumLength}, min: {length.MinimumLength})",
            EmailAddressAttribute => "EmailAddress",
            PhoneAttribute => "Phone",
            _ => attribute.GetType().Name
        };
    }
}