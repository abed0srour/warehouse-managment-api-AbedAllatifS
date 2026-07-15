namespace Warehouse.Application.Products.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record ArchiveProductCommand(Guid Id) : IRequest<bool>;

public class ArchiveProductCommandHandler : IRequestHandler<ArchiveProductCommand, bool>
{
    private readonly IProductRepository _productRepository;

    public ArchiveProductCommandHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id);
        if (product == null) return false;

        product.Archive();
        await _productRepository.UpdateAsync(product);
        return true;
    }
}