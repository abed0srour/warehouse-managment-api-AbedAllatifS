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
using Warehouse.Domain.Constants;
using Warehouse.Domain.Events;

public record UpdateProductQuantityCommand(Guid Id, int NewQuantity) : IRequest<Product?>;

public class UpdateProductQuantityCommandHandler : IRequestHandler<UpdateProductQuantityCommand, Product?>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<UpdateProductQuantityCommandHandler> _logger;

    public UpdateProductQuantityCommandHandler(
        IProductRepository productRepository,
        IMapper mapper,
        IDistributedCache cache,
        IEventPublisher eventPublisher,
        ILogger<UpdateProductQuantityCommandHandler> logger)
    {
        _productRepository = productRepository;
        _mapper = mapper;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Product?> Handle(UpdateProductQuantityCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null) return null;

        var previousQuantity = product.QuantityInStock;

        product.UpdateQuantity(request.NewQuantity);
        await _productRepository.UpdateAsync(product, cancellationToken);

        await _cache.RemoveAsync("products:all:True", cancellationToken);
        await _cache.RemoveAsync("products:all:False", cancellationToken);
        await _cache.RemoveAsync($"products:{request.Id}", cancellationToken);

        const int threshold = StockThresholds.LowStock;
        if (request.NewQuantity < threshold && previousQuantity >= threshold)
        {
            try
            {
                var stockLowEvent = new StockLowDetected(product.Id, product.Name, request.NewQuantity, threshold);
                await _eventPublisher.PublishAsync(stockLowEvent, "stock.low", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish stock.low event for product {ProductId}", product.Id);
            }
        }

        return product;
    }
}
