namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record UpdateProductQuantityCommand(Guid Id, int NewQuantity) : IRequest<Product?>;

public class UpdateProductQuantityCommandHandler : IRequestHandler<UpdateProductQuantityCommand, Product?>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;

    public UpdateProductQuantityCommandHandler(IProductRepository productRepository, IMapper mapper, IDistributedCache cache)
    {
        _productRepository = productRepository;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<Product?> Handle(UpdateProductQuantityCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null) return null;

        product.UpdateQuantity(request.NewQuantity);
        await _productRepository.UpdateAsync(product, cancellationToken);

        await _cache.RemoveAsync("products:all:True", cancellationToken);
        await _cache.RemoveAsync("products:all:False", cancellationToken);
        await _cache.RemoveAsync($"products:{request.Id}", cancellationToken);

        return product;
    }
}
