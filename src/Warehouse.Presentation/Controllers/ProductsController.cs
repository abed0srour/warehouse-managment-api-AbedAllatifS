using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Domain;
using Warehouse.Infrastructure.Repositories;

namespace Warehouse.Presentation.Controllers;

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int QuantityInStock { get; set; }
    public string? SupplierName { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class UpdateQuantityRequest
{
    public int QuantityInStock { get; set; }
}

public class UpdatePriceRequest
{
    public decimal Price { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ILogger<ProductsController> _logger;

    private const long MaxImageSizeBytes = 2 * 1024 * 1024; // 2 MB
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png" };

    public ProductsController(
        IProductRepository productRepository,
        ISupplierRepository supplierRepository,
        ILogger<ProductsController> logger)
    {
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
        _logger = logger;
    }

    // 1. GET /api/products
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAll([FromQuery] bool onlyAvailable = false)
    {
        var products = await _productRepository.GetAllAsync();

        var query = products.AsEnumerable();

        if (onlyAvailable)
        {
            query = query.Where(p => !p.IsArchived && p.QuantityInStock > 0);
        }

        var result = query.OrderByDescending(p => p.CreatedAt).ToList();

        return Ok(result);
    }

    // 2. GET /api/products/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> GetById(Guid id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} was not found." });
        }

        return Ok(product);
    }

    // 3. GET /api/products/search?name=...&supplier=...
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Product>>> Search(
        [FromQuery] string? name,
        [FromQuery] string? supplier)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(supplier))
        {
            return BadRequest(new { message = "At least one of 'name' or 'supplier' must be provided." });
        }

        var products = await _productRepository.GetAllAsync();

        var query = products.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(supplier))
        {
            query = query.Where(p =>
                p.SupplierName != null &&
                p.SupplierName.Contains(supplier, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(query.ToList());
    }

    // 4. POST /api/products
    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] CreateProductRequest request)
    {
        var existingProducts = await _productRepository.GetAllAsync();
        var duplicateSku = existingProducts.Any(p =>
            string.Equals(p.Sku, request.Sku, StringComparison.OrdinalIgnoreCase));

        if (duplicateSku)
        {
            return Conflict(new { message = $"A product with SKU '{request.Sku}' already exists." });
        }

        Product product;
        try
        {
            product = Product.Create(request.Name, request.Sku, request.Price, request.QuantityInStock);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        product.Description = request.Description;
        product.SupplierName = request.SupplierName;
        product.ExpiryDate = request.ExpiryDate;

        await _productRepository.AddAsync(product);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // 5. POST /api/products/{id}/quantity
    [HttpPost("{id:guid}/quantity")]
    public async Task<IActionResult> UpdateQuantity(Guid id, [FromBody] UpdateQuantityRequest request)
    {
        if (request.QuantityInStock < 0)
        {
            return BadRequest(new { message = "Quantity cannot be negative." });
        }

        var product = await _productRepository.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} was not found." });
        }

        try
        {
            product.UpdateQuantity(request.QuantityInStock);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await _productRepository.UpdateAsync(product);

        return Ok(product);
    }

    // 6. POST /api/products/{id}/price
    [HttpPost("{id:guid}/price")]
    public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdatePriceRequest request)
    {
        if (request.Price <= 0)
        {
            return BadRequest(new { message = "Price must be greater than zero." });
        }

        var product = await _productRepository.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} was not found." });
        }

        var oldPrice = product.Price;

        try
        {
            product.UpdatePrice(request.Price);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await _productRepository.UpdateAsync(product);

        _logger.LogInformation(
            "Product {ProductId} price changed from {OldPrice} to {NewPrice}",
            id, oldPrice, request.Price);

        return Ok(product);
    }

    // 7. POST /api/products/{id}/image
    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, [FromForm] IFormFile file)
    {
        var product = await _productRepository.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} was not found." });
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
            await file.CopyToAsync(stream);
        }

        return Ok(new { message = "Image uploaded successfully.", path = $"/uploads/{fileName}" });
    }

    // 8. DELETE /api/products/{id} - soft delete
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} was not found." });
        }

        product.Archive();

        await _productRepository.UpdateAsync(product);

        return NoContent();
    }

    // 9. GET /api/products/server-time
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
    [HttpPost("{id:guid}/assign-supplier/{supplierId:guid}")]
    public async Task<IActionResult> AssignSupplier(Guid id, Guid supplierId)
    {
        var product = await _productRepository.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} was not found." });
        }

        var supplier = await _supplierRepository.GetByIdAsync(supplierId);
        if (supplier == null)
        {
            return NotFound(new { message = $"Supplier with ID {supplierId} was not found." });
        }

        try
        {
            product.AssignSupplier(supplier);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        await _productRepository.UpdateAsync(product);

        return Ok(product);
    }
}