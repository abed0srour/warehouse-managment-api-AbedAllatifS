namespace Warehouse.Application.Products.Queries;

using AutoMapper;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetProductByIdQuery(Guid Id) : IRequest<ProductViewModel?>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductViewModel?>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;

    public GetProductByIdQueryHandler(IProductRepository productRepository, IMapper mapper, IDistributedCache cache)
    {
        _productRepository = productRepository;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ProductViewModel?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"products:{request.Id}";

        var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cachedValue != null)
        {
            return JsonSerializer.Deserialize<ProductViewModel?>(cachedValue);
        }

        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        var result = product == null ? null : _mapper.Map<ProductViewModel>(product);

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
