namespace Warehouse.Application.Products.Queries;

public record ExpiringSoonProductDto(
    Guid Id,
    string Name,
    string Sku,
    DateTime ExpiryDate,
    int DaysUntilExpiry,
    int QuantityInStock);
