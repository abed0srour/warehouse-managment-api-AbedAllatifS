using Microsoft.AspNetCore.Mvc;
using Warehouse.Domain;
using Warehouse.Infrastructure.Repositories;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierRepository _supplierRepository;

    public SuppliersController(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    // GET /api/suppliers
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetAll()
    {
        var suppliers = await _supplierRepository.GetAllAsync();
        return Ok(suppliers);
    }

    // GET /api/suppliers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Supplier>> GetById(Guid id)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null)
        {
            return NotFound(new { message = $"Supplier with ID {id} was not found." });
        }

        return Ok(supplier);
    }

    // POST /api/suppliers
    [HttpPost]
    public async Task<ActionResult<Supplier>> Create([FromBody] Supplier supplier)
    {
        supplier.Id = Guid.NewGuid();
        supplier.IsActive = true;

        await _supplierRepository.AddAsync(supplier);

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
    }

    // DELETE /api/suppliers/{id} - deactivate, not remove
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null)
        {
            return NotFound(new { message = $"Supplier with ID {id} was not found." });
        }

        supplier.IsActive = false;

        await _supplierRepository.UpdateAsync(supplier);

        return NoContent();
    }
}