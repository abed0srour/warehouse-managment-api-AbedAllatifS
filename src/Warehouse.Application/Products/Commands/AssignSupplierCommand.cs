namespace Warehouse.Application.Products.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record AssignSupplierCommand(Guid ProductId, Guid SupplierId) : IRequest<(bool Success, Product? Product)>;

public class AssignSupplierCommandHandler : IRequestHandler<AssignSupplierCommand, (bool Success, Product? Product)>
{
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;

    public AssignSupplierCommandHandler(IProductRepository productRepository, ISupplierRepository supplierRepository)
    {
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<(bool Success, Product? Product)> Handle(AssignSupplierCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId);
        if (product == null) return (false, null);

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId);
        if (supplier == null) return (false, null);

        product.AssignSupplier(supplier);
        await _productRepository.UpdateAsync(product);

        return (true, product);
    }
}
