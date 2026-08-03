namespace Warehouse.Application.Products.Queries;

using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetExpiringSoonProductsQuery(int WithinDays = 30) : IRequest<IEnumerable<ExpiringSoonProductDto>>;

public class GetExpiringSoonProductsQueryHandler : IRequestHandler<GetExpiringSoonProductsQuery, IEnumerable<ExpiringSoonProductDto>>
{
    private readonly IProductRepository _productRepository;

    public GetExpiringSoonProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<ExpiringSoonProductDto>> Handle(GetExpiringSoonProductsQuery request, CancellationToken cancellationToken)
    {
        if (request.WithinDays < 0)
        {
            throw new ArgumentException("WithinDays cannot be negative.");
        }

        var today = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(request.WithinDays);

        var products = await _productRepository.GetAllAsync(cancellationToken);

        return products
            .Where(p => !p.IsArchived
                && p.ExpiryDate.HasValue
                && p.ExpiryDate.Value.Date >= today
                && p.ExpiryDate.Value.Date <= cutoff)
            .OrderBy(p => p.ExpiryDate)
            .Select(p => new ExpiringSoonProductDto(
                p.Id,
                p.Name,
                p.Sku,
                p.ExpiryDate!.Value,
                (p.ExpiryDate.Value.Date - today).Days,
                p.QuantityInStock))
            .ToList();
    }
}
