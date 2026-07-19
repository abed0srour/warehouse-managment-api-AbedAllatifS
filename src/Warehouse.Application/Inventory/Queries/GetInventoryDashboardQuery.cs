namespace Warehouse.Application.Inventory.Queries;

using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record LowStockProductDto(Guid Id, string Name, int QuantityInStock);

public record InventoryDashboardDto(
    int TotalProducts,
    IReadOnlyList<LowStockProductDto> LowStockProducts,
    int TotalSuppliers);

public record GetInventoryDashboardQuery(int LowStockThreshold = 10) : IRequest<InventoryDashboardDto>;

public class GetInventoryDashboardQueryHandler : IRequestHandler<GetInventoryDashboardQuery, InventoryDashboardDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;

    public GetInventoryDashboardQueryHandler(
        IProductRepository productRepository,
        ISupplierRepository supplierRepository)
    {
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<InventoryDashboardDto> Handle(GetInventoryDashboardQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        var suppliers = await _supplierRepository.GetAllAsync(cancellationToken);

        var productList = products.ToList();

        var lowStock = productList
            .Where(p => !p.IsArchived && p.QuantityInStock < request.LowStockThreshold)
            .Select(p => new LowStockProductDto(p.Id, p.Name, p.QuantityInStock))
            .ToList();

        return new InventoryDashboardDto(productList.Count, lowStock, suppliers.Count());
    }
}
