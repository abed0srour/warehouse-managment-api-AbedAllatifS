using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warehouse.Infrastructure.Data;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IDbContextFactory<WarehouseDbContext> _contextFactory;

    public InventoryController(IDbContextFactory<WarehouseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
       
        var productStatsTask = GetProductStatsAsync();
        var supplierCountTask = GetSupplierCountAsync();

        await Task.WhenAll(productStatsTask, supplierCountTask);

        var productStats = await productStatsTask;
        var supplierCount = await supplierCountTask;

        return Ok(new
        {
            TotalProducts = productStats.Total,
            LowStockProducts = productStats.LowStock,
            TotalSuppliers = supplierCount
        });
    }

    private async Task<(int Total, List<object> LowStock)> GetProductStatsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var total = await context.Products.CountAsync();

        var lowStock = await context.Products
            .Where(p => !p.IsArchived && p.QuantityInStock < 10)
            .Select(p => new { p.Id, p.Name, p.QuantityInStock })
            .ToListAsync();

        return (total, lowStock.Cast<object>().ToList());
    }

    private async Task<int> GetSupplierCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Suppliers.CountAsync();
    }
}