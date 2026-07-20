namespace Warehouse.Application.Products.Queries;

using AutoMapper;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetAllProductsQuery(bool OnlyAvailable = false) : IRequest<IEnumerable<Product>>;

public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public GetAllProductsQueryHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ProductViewModel>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
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
