namespace Warehouse.Application.Products.Queries;

using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record SearchProductsQuery(string? Name, string? Supplier) : IRequest<IEnumerable<ProductViewModel>>;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, IEnumerable<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public SearchProductsQueryHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ProductViewModel>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        var query = products.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            query = query.Where(p => p.Name.Contains(request.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Supplier))
        {
            query = query.Where(p => (p.SupplierName ?? string.Empty).Contains(request.Supplier, StringComparison.OrdinalIgnoreCase));
        }

        return query.Select(_mapper.Map<ProductViewModel>).ToList();
    }
}
