namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
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

    public UpdateProductQuantityCommandHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<Product?> Handle(UpdateProductQuantityCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null) return null;

        product.UpdateQuantity(request.NewQuantity);
        await _productRepository.UpdateAsync(product, cancellationToken);
        return product;
    }
}
