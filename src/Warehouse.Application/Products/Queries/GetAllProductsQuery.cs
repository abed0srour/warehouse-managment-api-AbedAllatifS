namespace Warehouse.Application.Products.Queries;

using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetAllProductsQuery(bool OnlyAvailable = false) : IRequest<IEnumerable<Product>>;

public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<Product>>
{
    private readonly IProductRepository _productRepository;

    public GetAllProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<Product>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);

        var query = products.AsEnumerable();

        if (request.OnlyAvailable)
        {
            query = query.Where(p => !p.IsArchived && p.QuantityInStock > 0);
        }

        return query.OrderByDescending(p => p.CreatedAt).ToList();
    }
}
