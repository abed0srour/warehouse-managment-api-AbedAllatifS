namespace Warehouse.Application.Suppliers.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record DeactivateSupplierCommand(Guid Id) : IRequest<bool>;

public class DeactivateSupplierCommandHandler : IRequestHandler<DeactivateSupplierCommand, bool>
{
    private readonly ISupplierRepository _supplierRepository;

    public DeactivateSupplierCommandHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<bool> Handle(DeactivateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier == null) return false;

        supplier.IsActive = false;
        await _supplierRepository.UpdateAsync(supplier, cancellationToken);
        return true;
    }
}
