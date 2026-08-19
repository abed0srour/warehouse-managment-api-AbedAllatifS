namespace Warehouse.Application.Products.Queries;

public record OutOfStockProductDto(
    Guid Id,
    string Name,
    string Sku,
    Guid? SupplierId,
    string? SupplierName,
    DateTime? LastUpdatedAt);
