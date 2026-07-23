namespace Warehouse.Application.Products.Queries;

using AutoMapper;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetAllProductsQuery(bool OnlyAvailable = false) : IRequest<IEnumerable<ProductViewModel>>;

public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, IEnumerable<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;

    public GetAllProductsQueryHandler(IProductRepository productRepository, IMapper mapper, IDistributedCache cache)
    {
        _productRepository = productRepository;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<IEnumerable<ProductViewModel>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"products:all:{request.OnlyAvailable}";

        var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cachedValue != null)
        {
            return JsonSerializer.Deserialize<IEnumerable<ProductViewModel>>(cachedValue) ?? Enumerable.Empty<ProductViewModel>();
        }

        var products = await _productRepository.GetAllAsync(cancellationToken);

        var query = products.AsEnumerable();

        if (request.OnlyAvailable)
        {
            query = query.Where(p => !p.IsArchived && p.QuantityInStock > 0);
        }

        var ordered = query.OrderByDescending(p => p.CreatedAt).ToList();

        var result = _mapper.Map<IEnumerable<ProductViewModel>>(ordered);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            },
            cancellationToken);

        return result;
    }
}
