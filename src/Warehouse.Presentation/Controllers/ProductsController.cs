using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common;
using Warehouse.Application.Products;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Warehouse.Application.Resources;
using Warehouse.Domain;
using Warehouse.Application.Products.Commands;
using Warehouse.Application.Products.Queries;
using WarehouseManagement.Api.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILogger<ProductsController> _logger;


    private const long MaxImageSizeBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png" };

    public ProductsController(IMediator mediator, IStringLocalizer<SharedResources> localizer, ILogger<ProductsController> logger)
    {
      _mediator = mediator;
      _localizer = localizer;
      _logger = logger;
    }

    // 1. GET /api/products
    [Authorize(Policy = "AuthenticatedUser")] 
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductViewModel>>> GetAll([FromQuery] bool onlyAvailable = false, CancellationToken cancellationToken = default)
    {
        var products = await _mediator.Send(new GetAllProductsQuery(onlyAvailable), cancellationToken);
        return Ok(products);
    }

    // 2. GET /api/products/{id}
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductViewModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        if (product == null)
        {
            _logger.LogWarning("Product {ProductId} was not found", id);
            return NotFound(new { message = _localizer["ProductNotFound"].Value });
        }

        return Ok(product);
    }

    // 3. GET /api/products/search?name=...&supplier=...
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ProductViewModel>>> Search(
        [FromQuery] string? name,
        [FromQuery] string? supplier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(supplier))
        {
            return BadRequest(new { message = "At least one of 'name' or 'supplier' must be provided." });
        }

        var products = await _mediator.Send(new SearchProductsQuery(name, supplier), cancellationToken);
        return Ok(products);
    }

    // 4. POST /api/products
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        Product product;
        try
        {
            product = await _mediator.Send(new CreateProductCommand(
                request.Name,
                request.SKU,
                request.Description,
                request.Price,
                request.QuantityInStock,
                request.SupplierName,
                request.ExpiryDate), cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation("Product {ProductId} created with SKU {Sku}", product.Id, product.Sku);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // 5. POST /api/products/{id}/quantity
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] UpdateProductQuantityRequest request, CancellationToken cancellationToken)
    {
        Product? product;
        try
        {
            product = await _mediator.Send(new UpdateProductQuantityCommand(id, request.QuantityInStock), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (product == null)
        {
            return NotFound(new { message = _localizer["ProductNotFound"].Value });
        }

        _logger.LogInformation("Product {ProductId} quantity updated to {Quantity}", id, request.QuantityInStock);

        return Ok(product);
    }

    // 6. POST /api/products/{id}/price
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/price")]
    public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdateProductPriceRequest request, CancellationToken cancellationToken)
    {
        Product? product;
        try
        {
            product = await _mediator.Send(new UpdateProductPriceCommand(id, request.Price), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (product == null)
        {
            return NotFound(new { message = _localizer["ProductNotFound"].Value });
        }

        _logger.LogInformation("Product {ProductId} price updated to {Price}", id, request.Price);

        return Ok(product);
    }

    // 7. POST /api/products/{id}/image
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        if (product == null)
        {
            return NotFound(new { message = _localizer["ProductNotFound"].Value });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "A file is required." });
        }

        if (file.Length > MaxImageSizeBytes)
        {
            return BadRequest(new { message = "File size cannot exceed 2 MB." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only .jpg and .png files are allowed." });
        }

        var uploadsFolder = Path.Combine("wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{id}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return Ok(new { message = "Image uploaded successfully.", path = $"/uploads/{fileName}" });
    }

    // 8. DELETE /api/products/{id} - soft delete
    [Authorize(Policy = "AdminOnly")] 
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ArchiveProductCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error!.Type switch
            {
                ErrorType.NotFound => NotFound(new { message = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { message = result.Error.Message }),
                _ => BadRequest(new { message = result.Error.Message })
            };
        }

        _logger.LogInformation("Product {ProductId} archived", id);

        return NoContent();
    }

    // 9. GET /api/products/server-time
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("server-time")]
    public IActionResult GetServerTime([FromHeader(Name = "Accept-Language")] string? acceptLanguage)
    {
        var culture = (acceptLanguage?.Split(',').FirstOrDefault()?.Trim()) switch
        {
            "fr-FR" => new CultureInfo("fr-FR"),
            "ar-LB" => new CultureInfo("ar-LB"),
            "en-US" => new CultureInfo("en-US"),
            _ => new CultureInfo("en-US")
        };

        var formatted = DateTime.UtcNow.ToString("F", culture);

        return Ok(new { serverTimeUtc = formatted, culture = culture.Name });
    }

    // Task 2 — POST /api/products/{id}/assign-supplier/{supplierId}
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/assign-supplier/{supplierId:guid}")]
    public async Task<IActionResult> AssignSupplier(Guid id, Guid supplierId, CancellationToken cancellationToken)
    {
        Product product;
        try
        {
            product = await _mediator.Send(new AssignSupplierCommand(id, supplierId), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        _logger.LogInformation("Supplier {SupplierId} assigned to product {ProductId}", supplierId, id);

        return Ok(product);
    }
}
