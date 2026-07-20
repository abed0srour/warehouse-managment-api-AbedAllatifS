namespace Warehouse.Application.Suppliers.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record DeactivateSupplierCommand(Guid Id) : IRequest<Result>;

public class DeactivateSupplierCommandHandler : IRequestHandler<DeactivateSupplierCommand, Result>
{
    private readonly ISupplierRepository _supplierRepository;

    public DeactivateSupplierCommandHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<Result> Handle(DeactivateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier == null) return false;

        supplier.IsActive = false;
        await _supplierRepository.UpdateAsync(supplier, cancellationToken);
        return true;
    }
}
