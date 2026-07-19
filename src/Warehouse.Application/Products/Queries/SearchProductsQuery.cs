namespace Warehouse.Application.Products.Queries;

using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record SearchProductsQuery(string? Name, string? Supplier) : IRequest<IEnumerable<Product>>;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, IEnumerable<Product>>
{
    private readonly IProductRepository _productRepository;

    public SearchProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<Product>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
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

        return query.ToList();
    }
}
