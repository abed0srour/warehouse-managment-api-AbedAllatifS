using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

// 1. Handler for Getting Products by Supplier Name
public class GetProductsBySupplierQueryHandler : IRequestHandler<GetProductsBySupplierQuery, IEnumerable<ProductViewModel>>
{
    private readonly WarehouseDbContext _context;
    private readonly IMapper _mapper;

    public GetProductsBySupplierQueryHandler(WarehouseDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ProductViewModel>> Handle(GetProductsBySupplierQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Supplier)
            .Where(p => p.SupplierName == request.SupplierName || (p.Supplier != null && p.Supplier.Name == request.SupplierName));

        query = request.SortOrder.ToLower() == "asc"
            ? query.OrderBy(p => p.CreatedAt)
            : query.OrderByDescending(p => p.CreatedAt);

        return await query.ProjectTo<ProductViewModel>(_mapper.ConfigurationProvider).ToListAsync(cancellationToken);
    }
}

// 2. Handler for Grouping by Expiry Year[cite: 3]
public class GetProductsGroupedByExpiryYearQueryHandler : IRequestHandler<GetProductsGroupedByExpiryYearQuery, IEnumerable<ExpiryYearGroupDto>>
{
    private readonly WarehouseDbContext _context;

    public GetProductsGroupedByExpiryYearQueryHandler(WarehouseDbContext context) => _context = context;

    public async Task<IEnumerable<ExpiryYearGroupDto>> Handle(GetProductsGroupedByExpiryYearQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(p => p.ExpiryDate != null)
            .GroupBy(p => p.ExpiryDate!.Value.Year)
            .Select(g => new ExpiryYearGroupDto(
                g.Key,
                g.Count(),
                g.Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Description = p.Description ?? string.Empty,
                    Price = p.Price,
                    QuantityInStock = p.QuantityInStock,
                    SupplierName = p.SupplierName,
                    ExpiryDate = p.ExpiryDate,
                    IsArchived = p.IsArchived,
                    CreatedAt = p.CreatedAt,
                    LastUpdatedAt = p.LastUpdatedAt
                }).ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}

// 3. Handler for Grouping by Expiry Year and Supplier Country[cite: 3]
public class GetProductsGroupedByExpiryAndCountryQueryHandler : IRequestHandler<GetProductsGroupedByExpiryAndCountryQuery, IEnumerable<ExpiryYearAndCountryGroupDto>>
{
    private readonly WarehouseDbContext _context;

    public GetProductsGroupedByExpiryAndCountryQueryHandler(WarehouseDbContext context) => _context = context;

    public async Task<IEnumerable<ExpiryYearAndCountryGroupDto>> Handle(GetProductsGroupedByExpiryAndCountryQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(p => p.ExpiryDate != null && p.Supplier != null)
            .GroupBy(p => new { Year = p.ExpiryDate!.Value.Year, gCountry = p.Supplier!.Country })
            .Select(g => new ExpiryYearAndCountryGroupDto(
                g.Key.Year,
                g.Key.gCountry,
                g.Count(),
                g.Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Description = p.Description ?? string.Empty,
                    Price = p.Price,
                    QuantityInStock = p.QuantityInStock,
                    SupplierName = p.SupplierName,
                    ExpiryDate = p.ExpiryDate,
                    IsArchived = p.IsArchived,
                    CreatedAt = p.CreatedAt,
                    LastUpdatedAt = p.LastUpdatedAt
                }).ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}

// 4. Handler for Total Product Count[cite: 3]
public class GetTotalProductCountQueryHandler : IRequestHandler<GetTotalProductCountQuery, int>
{
    private readonly WarehouseDbContext _context;

    public GetTotalProductCountQueryHandler(WarehouseDbContext context) => _context = context;

    public async Task<int> Handle(GetTotalProductCountQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products.CountAsync(cancellationToken);
    }
}

// 5. Handler for Server-Side Pagination[cite: 3]
public class GetPagedProductsQueryHandler : IRequestHandler<GetPagedProductsQuery, PagedResponseDto<ProductViewModel>>
{
    private readonly WarehouseDbContext _context;
    private readonly IMapper _mapper;

    public GetPagedProductsQueryHandler(WarehouseDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PagedResponseDto<ProductViewModel>> Handle(GetPagedProductsQuery request, CancellationToken cancellationToken)
    {
        var totalCount = await _context.Products.CountAsync(cancellationToken);

        var items = await _context.Products
            .OrderBy(p => p.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectTo<ProductViewModel>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return new PagedResponseDto<ProductViewModel>(items, request.PageNumber, request.PageSize, totalCount);
    }
}
