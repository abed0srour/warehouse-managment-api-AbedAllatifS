namespace Warehouse.Application.Products.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record UpdateProductQuantityCommand(Guid Id, int NewQuantity) : IRequest<bool>;

public class UpdateProductQuantityCommandHandler : IRequestHandler<UpdateProductQuantityCommand, bool>
{
    private readonly IProductRepository _productRepository;

    public UpdateProductQuantityCommandHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> Handle(UpdateProductQuantityCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id);
        if (product == null) return false;

        product.UpdateQuantity(request.NewQuantity);
        await _productRepository.UpdateAsync(product);
        return true;
    }
}