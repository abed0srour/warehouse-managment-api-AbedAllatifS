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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetAll()
    {
        var suppliers = await _supplierRepository.GetAllAsync();
        return Ok(suppliers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Supplier>> GetById(Guid id)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null) return NotFound();

        return Ok(supplier);
    }

    [HttpPost]
    public async Task<ActionResult<Supplier>> Create([FromBody] Supplier supplier)
    {
        await _supplierRepository.AddAsync(supplier);
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Supplier supplier)
    {
        if (id != supplier.Id) return BadRequest("ID mismatch");

        var existing = await _supplierRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _supplierRepository.UpdateAsync(supplier);
        return NoContent();
    }
}