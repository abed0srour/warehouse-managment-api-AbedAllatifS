using Microsoft.AspNetCore.Mvc;
using System;
using WarehouseManagement.Api.Services;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Controllers
{
    [ApiController]
    [Route("api/suppliers")]
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        // The service is automatically injected via the constructor
        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        // 1. GET ALL SUPPLIERS
        // GET /api/suppliers
        [HttpGet]
        public IActionResult GetAllSuppliers()
        {
            var suppliers = _supplierService.GetAll();
            return Ok(suppliers);
        }

        // 2. GET SUPPLIER BY ID
        // GET /api/suppliers/{id}
        [HttpGet("{id:guid}")]
        public IActionResult GetSupplierById([FromRoute] Guid id)
        {
            var supplier = _supplierService.GetById(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return Ok(supplier);
        }

        // 3. POST CREATE SUPPLIER
        // POST /api/suppliers
        [HttpPost]
        public IActionResult CreateSupplier([FromBody] CreateSupplierRequest request)
        {
            var newSupplier = _supplierService.Create(request);
            
            return CreatedAtAction(nameof(GetSupplierById), new { id = newSupplier.Id }, newSupplier);
        }

        // 4. DELETE DEACTIVATE SUPPLIER
        // DELETE /api/suppliers/{id}
        [HttpDelete("{id:guid}")]
        public IActionResult DeactivateSupplier([FromRoute] Guid id)
        {
            var wasDeactivated = _supplierService.Deactivate(id);
            if (!wasDeactivated)
            {
                return NotFound();
            }
            
            return NoContent();
        }
    }
}