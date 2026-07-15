namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record AssignSupplierCommand(Guid ProductId, Guid SupplierId) : IRequest<Result<ProductViewModel>>;

public class AssignSupplierCommandHandler : IRequestHandler<AssignSupplierCommand, Result<ProductViewModel>>
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

    public async Task<Result<ProductViewModel>> Handle(AssignSupplierCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductViewModel>(ErrorType.NotFound, $"Product with ID {request.ProductId} was not found.");
        }

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure<ProductViewModel>(ErrorType.NotFound, $"Supplier with ID {request.SupplierId} was not found.");
        }

        try
        {
            product.AssignSupplier(supplier);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ProductViewModel>(ErrorType.Conflict, ex.Message);
        }

        await _productRepository.UpdateAsync(product, cancellationToken);
        return Result.Success(_mapper.Map<ProductViewModel>(product));
    }
}
