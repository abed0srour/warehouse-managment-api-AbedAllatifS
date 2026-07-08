namespace Warehouse.Application.Products.Queries;

using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetAllProductsQuery() : IRequest<IEnumerable<Product>>;

public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<Product>>
{
    private readonly IProductRepository _productRepository;

    public GetAllProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<Product>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetAllAsync();
    }
}