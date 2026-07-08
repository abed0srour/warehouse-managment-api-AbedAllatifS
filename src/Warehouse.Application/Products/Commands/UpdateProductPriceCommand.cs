namespace Warehouse.Application.Products.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record UpdateProductPriceCommand(Guid Id, decimal NewPrice) : IRequest<bool>;

public class UpdateProductPriceCommandHandler : IRequestHandler<UpdateProductPriceCommand, bool>
{
    private readonly IProductRepository _productRepository;

    public UpdateProductPriceCommandHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id);
        if (product == null) return false;

        product.UpdatePrice(request.NewPrice);
        await _productRepository.UpdateAsync(product);
        return true;
    }
}