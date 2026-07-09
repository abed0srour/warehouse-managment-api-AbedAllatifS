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
using WarehouseManagement.Api.Services;

namespace WarehouseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> _logger;
        private readonly IProductService _productService;
        private readonly ISupplierService _supplierService;

        public ProductsController(
            ILogger<ProductsController> logger,
            IProductService productService,
            ISupplierService supplierService)
        {
            _logger = logger;
            _productService = productService;
            _supplierService = supplierService;
        }

        // 1. GET ALL PRODUCTS
        [HttpGet]
        public IActionResult GetProducts([FromQuery] bool onlyAvailable = false)
        {
            var products = _productService.GetAll(onlyAvailable).ToList();
            return Ok(products);
        }

        // 2. GET PRODUCT BY ID
        [HttpGet("{id:guid}")]
        public IActionResult GetProductById([FromRoute] Guid id)
        {
            var product = _productService.GetById(id);
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
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(supplier))
            {
                return BadRequest("At least one search parameter ('name' or 'supplier') must be provided.");
            }

            var products = _productService.Search(name, supplier).ToList();
            return Ok(products);
        }

        // 4. CREATE PRODUCT
        [HttpPost]
        public IActionResult CreateProduct([FromBody] CreateProductRequest request)
        {
            if (_productService.SkuExists(request.SKU))
            {
                return BadRequest("A product with this SKU already exists.");
            }

            var newProduct = _productService.Create(request);
            return CreatedAtAction(nameof(GetProductById), new { id = newProduct.Id }, newProduct);
        }

        // 5. UPDATE QUANTITY
        [HttpPost("{id:guid}/quantity")]
        public IActionResult UpdateQuantity([FromRoute] Guid id, [FromBody] UpdateProductQuantityRequest request)
        {
            if (request.QuantityInStock < 0)
            {
                return BadRequest("Quantity cannot be negative.");
            }

            if (!_productService.UpdateQuantity(id, request.QuantityInStock, out var product))
            {
                return NotFound();
            }

            return Ok(product);
        }

        // 6. UPDATE PRICE
        [HttpPost("{id:guid}/price")]
        public IActionResult UpdatePrice([FromRoute] Guid id, [FromBody] UpdateProductPriceRequest request)
        {
            if (request.Price <= 0)
            {
                return BadRequest("Price must be greater than zero.");
            }

            if (!_productService.UpdatePrice(id, request.Price, out var oldPrice, out var product))
            {
                return NotFound();
            }

            _logger.LogInformation("Product {ProductId} price updated. Old Price: {OldPrice}, New Price: {NewPrice}", id, oldPrice, request.Price);
            return Ok(product);
        }

        // 7. UPLOAD IMAGE
        [HttpPost("{id:guid}/image")]
        public async Task<IActionResult> UploadImage([FromRoute] Guid id, [FromForm] IFormFile file)
        {
            var product = _productService.GetById(id);
            if (product == null)
            {
                return NotFound();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            if (file.Length > 2 * 1024 * 1024)
            {
                return BadRequest("File size exceeds the maximum limit of 2 MB.");
            }

            var extension = file.FileName.ToLowerSuffix();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                return BadRequest("Invalid image format. Only JPG, JPEG, and PNG are allowed.");
            }

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

            FakeWarehouseStore.ProductImages.Add(new ProductImage
            {
                ProductId = id,
                FileName = uniqueFileName,
                FilePath = Path.Combine("uploads", uniqueFileName)
            });

            product.LastUpdatedAt = DateTime.UtcNow;
            return Ok(new { Message = "Image uploaded successfully", FileName = uniqueFileName });
        }

        // 8. DELETE PRODUCT (SOFT DELETE)
        [HttpDelete("{id:guid}")]
        public IActionResult SoftDeleteProduct([FromRoute] Guid id)
        {
            if (!_productService.SoftDelete(id))
            {
                return NotFound();
            }

            return NoContent();
        }

        // 9. GET WAREHOUSE SERVER TIME
        [HttpGet("server-time")]
        public IActionResult GetServerTime([FromHeader(Name = "Accept-Language")] string? acceptLanguage)
        {
            var cultureInfo = new CultureInfo("en-US");

            if (!string.IsNullOrWhiteSpace(acceptLanguage))
            {
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

            var formattedDate = DateTime.Now.ToString("F", cultureInfo);
            return Ok(new { ServerTime = formattedDate, CultureUsed = cultureInfo.Name });
        }

        [HttpPost("{id:guid}/assign-supplier/{supplierId:guid}")]
        public IActionResult AssignSupplier([FromRoute] Guid id, [FromRoute] Guid supplierId)
        {
            var product = _productService.GetById(id);
            if (product == null)
            {
                return NotFound($"Product with ID {id} was not found.");
            }

            if (product.IsArchived)
            {
                return BadRequest("Cannot assign a supplier to an archived product.");
            }

            var supplier = _supplierService.GetById(supplierId);
            if (supplier == null)
            {
                return NotFound($"Supplier with ID {supplierId} was not found.");
            }

            _productService.AssignSupplier(id, supplier, out _);
            return Ok(new
            {
                Message = "Supplier assigned successfully.",
                ProductId = product.Id,
                ProductName = product.Name,
                AssignedSupplier = supplier.Name
            });
        }
    }

    public static class StringExtensions
    {
        public static string ToLowerSuffix(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return Path.GetExtension(value).ToLower(CultureInfo.InvariantCulture);
        }
    }
}
