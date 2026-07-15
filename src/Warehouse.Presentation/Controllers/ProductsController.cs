using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Warehouse.Application.Products.Commands;
using Warehouse.Application.Products.Queries;
using Warehouse.Domain;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> _logger;
        private readonly IMediator _mediator;

        public ProductsController(
            ILogger<ProductsController> logger,
            IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;
        }

        // 1. GET ALL PRODUCTS
        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] bool onlyAvailable = false)
        {
            var products = await _mediator.Send(new GetAllProductsQuery());
            var filtered = onlyAvailable ? products.Where(p => p.QuantityInStock > 0).ToList() : products.ToList();
            return Ok(filtered);
        }

        // 2. GET PRODUCT BY ID
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProductById([FromRoute] Guid id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        // 3. SEARCH PRODUCTS
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string? name, [FromQuery] string? supplier)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(supplier))
            {
                return BadRequest("At least one search parameter ('name' or 'supplier') must be provided.");
            }

            var products = await _mediator.Send(new SearchProductsQuery(name, supplier));
            return Ok(products.ToList());
        }

        // 4. CREATE PRODUCT
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            try
            {
                var productId = await _mediator.Send(new CreateProductCommand(request.Name, request.SKU, request.Description, request.Price, request.QuantityInStock, request.ExpiryDate));
                var createdProduct = await _mediator.Send(new GetProductByIdQuery(productId));
                return CreatedAtAction(nameof(GetProductById), new { id = productId }, createdProduct);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 5. UPDATE QUANTITY
        [HttpPost("{id:guid}/quantity")]
        public async Task<IActionResult> UpdateQuantity([FromRoute] Guid id, [FromBody] UpdateProductQuantityRequest request)
        {
            try
            {
                var success = await _mediator.Send(new UpdateProductQuantityCommand(id, request.QuantityInStock));
                if (!success)
                {
                    return NotFound();
                }

                var product = await _mediator.Send(new GetProductByIdQuery(id));
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 6. UPDATE PRICE
        [HttpPost("{id:guid}/price")]
        public async Task<IActionResult> UpdatePrice([FromRoute] Guid id, [FromBody] UpdateProductPriceRequest request)
        {
            try
            {
                var success = await _mediator.Send(new UpdateProductPriceCommand(id, request.Price));
                if (!success)
                {
                    return NotFound();
                }

                _logger.LogInformation("Product {ProductId} price updated. New Price: {NewPrice}", id, request.Price);
                var product = await _mediator.Send(new GetProductByIdQuery(id));
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:guid}/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromRoute] Guid id, [FromForm] UploadImageRequest request)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
            {
                return NotFound();
            }

            var file = request.File;
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

            product.LastUpdatedAt = DateTime.UtcNow;
            return Ok(new { Message = "Image uploaded successfully", FileName = uniqueFileName });
        }

        // 8. DELETE PRODUCT (SOFT DELETE)
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> SoftDeleteProduct([FromRoute] Guid id)
        {
            var success = await _mediator.Send(new ArchiveProductCommand(id));
            if (!success)
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
        public async Task<IActionResult> AssignSupplier([FromRoute] Guid id, [FromRoute] Guid supplierId)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
            {
                return NotFound($"Product with ID {id} was not found.");
            }

            if (product.IsArchived)
            {
                return BadRequest("Cannot assign a supplier to an archived product.");
            }

            try
            {
                var result = await _mediator.Send(new AssignSupplierCommand(id, supplierId));
                if (!result.Success || result.Product == null)
                {
                    return NotFound($"Supplier with ID {supplierId} was not found.");
                }

                return Ok(new
                {
                    Message = "Supplier assigned successfully.",
                    ProductId = result.Product.Id,
                    ProductName = result.Product.Name,
                    AssignedSupplier = result.Product.SupplierName
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
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
