using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

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
