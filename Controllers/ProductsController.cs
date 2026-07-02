using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WarehouseManagement.Api.Data;
using WarehouseManagement.Api.Models;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> _logger;

        // Injecting the standard logger for Endpoint #6 (Audit Logs)
        public ProductsController(ILLogger<ProductsController> logger)
        {
            _logger = logger;
        }

        // 1. GET ALL PRODUCTS
        [HttpGet]
        public IActionResult GetProducts([FromQuery] bool onlyAvailable = false)
        {
            var query = FakeWarehouseStore.Products.AsEnumerable();

            if (onlyAvailable)
            {
                query = query.Where(p => p.QuantityInStock > 0);
            }

            var products = query.OrderByDescending(p => p.CreatedAt).ToList();
            return Ok(products);
        }

        // 2. GET PRODUCT BY ID
        [HttpGet("{id:guid}")]
        public IActionResult GetProductById([FromRoute] Guid id)
        {
            var product = FakeWarehouseStore.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        // 3. SEARCH PRODUCTS
        [HttpGet("search")]
        public IActionResult SearchProducts([FromQuery] string? name, [FromQuery] string? supplier)
        {
            // Harder requirement: if both parameters empty -> return bad request
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(supplier))
            {
                return BadRequest("At least one search parameter ('name' or 'supplier') must be provided.");
            }

            var query = FakeWarehouseStore.Products.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(supplier))
            {
                query = query.Where(p => p.SupplierName.Contains(supplier, StringComparison.OrdinalIgnoreCase));
            }

            return Ok(query.ToList());
        }

        // 4. CREATE PRODUCT
        [HttpPost]
        public IActionResult CreateProduct([FromBody] CreateProductRequest request)
        {
            // Harder requirement: prevent duplicate SKU using manual logic only
            bool skuExists = FakeWarehouseStore.Products.Any(p => p.SKU.Equals(request.SKU, StringComparison.OrdinalIgnoreCase));
            if (skuExists)
            {
                return BadRequest("A product with this SKU already exists.");
            }

            var newProduct = new Product
            {
                Id = Guid.NewGuid(), 
                Name = request.Name,
                SKU = request.SKU,
                Description = request.Description,
                Price = request.Price,
                QuantityInStock = request.QuantityInStock,
                SupplierName = request.SupplierName,
                ExpiryDate = request.ExpiryDate,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow 
            };

            FakeWarehouseStore.Products.Add(newProduct);

            return CreatedAtAction(nameof(GetProductById), new { id = newProduct.Id }, newProduct);
        }

        // 5. UPDATE QUANTITY
        [HttpPost("{id:guid}/quantity")]
        public IActionResult UpdateQuantity([FromRoute] Guid id, [FromBody] UpdateProductQuantityRequest request)
        {
            var product = FakeWarehouseStore.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            // Rule: quantity cannot be negative
            if (request.QuantityInStock < 0)
            {
                return BadRequest("Quantity cannot be negative.");
            }

            product.QuantityInStock = request.QuantityInStock;
            product.LastUpdatedAt = DateTime.UtcNow; // last updated date changes

            return Ok(product);
        }

        // 6. UPDATE PRICE
        [HttpPost("{id:guid}/price")]
        public IActionResult UpdatePrice([FromRoute] Guid id, [FromBody] UpdateProductPriceRequest request)
        {
            var product = FakeWarehouseStore.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            // Rule: price must be > 0
            if (request.Price <= 0)
            {
                return BadRequest("Price must be greater than zero.");
            }

            decimal oldPrice = product.Price;
            product.Price = request.Price;
            product.LastUpdatedAt = DateTime.UtcNow;

            // Rule: audit old/new value in logs
            _logger.LogInformation("Product {ProductId} price updated. Old Price: {OldPrice}, New Price: {NewPrice}", id, oldPrice, request.Price);

            return Ok(product);
        }

        // 7. UPLOAD IMAGE
        [HttpPost("{id:guid}/image")]
        public async Task<IActionResult> UploadImage([FromRoute] Guid id, [FromForm] IFormFile file)
        {
            var product = FakeWarehouseStore.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            // Requirement: max 2 MB (2 * 1024 * 1024 bytes)
            if (file.Length > 2 * 1024 * 1024)
            {
                return BadRequest("File size exceeds the maximum limit of 2 MB.");
            }

            // Requirement: only jpg/png
            var extension = Path.GetExtension(file.FileName).ToLowerSuffix();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                return BadRequest("Invalid image format. Only JPG, JPEG, and PNG are allowed.");
            }

            // Requirement: save to wwwroot/uploads
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // attach the web relative path back into the object if needed
            product.LastUpdatedAt = DateTime.UtcNow;

            return Ok(new { Message = "Image uploaded successfully", FileName = uniqueFileName });
        }

        // 8. DELETE PRODUCT (SOFT DELETE)
        [HttpDelete("{id:guid}")]
        public IActionResult SoftDeleteProduct([FromRoute] Guid id)
        {
            var product = FakeWarehouseStore.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            // Harder requirement: Do soft delete only (set IsArchived = true, do not remove from list)
            product.IsArchived = true;
            product.LastUpdatedAt = DateTime.UtcNow;

            return NoContent();
        }

        // 9. GET WAREHOUSE SERVER TIME
        [HttpGet("server-time")]
        public IActionResult GetServerTime([FromHeader(Name = "Accept-Language")] string? acceptLanguage)
        {
            // Default culture fallback if header is empty or invalid
            var cultureInfo = new CultureInfo("en-US");

            if (!string.IsNullOrWhiteSpace(acceptLanguage))
            {
                // Parse the first language token if a comma separated list is provided (e.g., 'en-US,en;q=0.9')
                var primaryLanguage = acceptLanguage.Split(',')[0].Trim();

                if (primaryLanguage.Equals("fr-FR", StringComparison.OrdinalIgnoreCase) || 
                    primaryLanguage.Equals("fr", StringComparison.OrdinalIgnoreCase))
                {
                    cultureInfo = new CultureInfo("fr-FR");
                }
                else if (primaryLanguage.Equals("ar-LB", StringComparison.OrdinalIgnoreCase) || 
                         primaryLanguage.Equals("ar", StringComparison.OrdinalIgnoreCase))
                {
                    cultureInfo = new CultureInfo("ar-LB");
                }
            }

            // Format date string explicitly according to target language rules
            var formattedDate = DateTime.Now.ToString("F", cultureInfo);

            return Ok(new { ServerTime = formattedDate, CultureUsed = cultureInfo.Name });
        }
    }

    // Quick helper extension method for string parsing
    public static class StringExtensions
    {
        public static string ToLowerSuffix(this string value) => value?.ToLower(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}