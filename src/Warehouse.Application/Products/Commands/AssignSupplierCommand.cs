namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common.Exceptions;
using Warehouse.Domain;

public record AssignSupplierCommand(Guid ProductId, Guid SupplierId) : IRequest<Product>;

public class AssignSupplierCommandHandler : IRequestHandler<AssignSupplierCommand, Product>
{
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IMapper _mapper;

    public AssignSupplierCommandHandler(IProductRepository productRepository, ISupplierRepository supplierRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
        _mapper = mapper;
    }

    public async Task<Product> Handle(AssignSupplierCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException($"Product with ID {request.ProductId} was not found.");
        }

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier == null)
        {
            throw new NotFoundException($"Supplier with ID {request.SupplierId} was not found.");
        }

        product.AssignSupplier(supplier);
        await _productRepository.UpdateAsync(product, cancellationToken);

        return product;
    }
}
