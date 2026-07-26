namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record UpdateProductPriceCommand(Guid Id, decimal NewPrice) : IRequest<Product?>;

public class UpdateProductPriceCommandHandler : IRequestHandler<UpdateProductPriceCommand, Product?>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<UpdateProductPriceCommandHandler> _logger;
    private readonly IDistributedCache _cache;

    public UpdateProductPriceCommandHandler(
        IProductRepository productRepository,
        ILogger<UpdateProductPriceCommandHandler> logger,
        IDistributedCache cache)
    {
        _productRepository = productRepository;
        _logger = logger;
        _cache = cache;
    }

    public async Task<Product?> Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null) return null;

        var oldPrice = product.Price;

        product.UpdatePrice(request.NewPrice);
        await _productRepository.UpdateAsync(product, cancellationToken);

        await _cache.RemoveAsync("products:all:True", cancellationToken);
        await _cache.RemoveAsync("products:all:False", cancellationToken);
        await _cache.RemoveAsync($"products:{request.Id}", cancellationToken);

        _logger.LogInformation(
            "Product {ProductId} price changed from {OldPrice} to {NewPrice}",
            request.Id, oldPrice, request.NewPrice);

        return product;
    }
}
