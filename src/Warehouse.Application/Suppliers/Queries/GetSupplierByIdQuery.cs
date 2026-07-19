namespace Warehouse.Application.Suppliers.Queries;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetSupplierByIdQuery(Guid Id) : IRequest<Supplier?>;

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, Supplier?>
{
    private readonly ISupplierRepository _supplierRepository;

    public GetSupplierByIdQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<Supplier?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        return await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
    }
}