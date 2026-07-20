namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
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

    public UpdateProductPriceCommandHandler(
        IProductRepository productRepository,
        ILogger<UpdateProductPriceCommandHandler> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<Product?> Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null) return null;

        var oldPrice = product.Price;

        product.UpdatePrice(request.NewPrice);
        await _productRepository.UpdateAsync(product, cancellationToken);

        _logger.LogInformation(
            "Product {ProductId} price changed from {OldPrice} to {NewPrice}",
            request.Id, oldPrice, request.NewPrice);

        return product;
    }
}
