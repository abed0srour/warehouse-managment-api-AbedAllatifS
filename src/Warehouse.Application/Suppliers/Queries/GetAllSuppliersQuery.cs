namespace Warehouse.Application.Suppliers.Queries;

using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetAllSuppliersQuery() : IRequest<IEnumerable<Supplier>>;

public class GetAllSuppliersQueryHandler : IRequestHandler<GetAllSuppliersQuery, IEnumerable<Supplier>>
{
    private readonly ISupplierRepository _supplierRepository;

    public GetAllSuppliersQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<IEnumerable<Supplier>> Handle(GetAllSuppliersQuery request, CancellationToken cancellationToken)
    {
        return await _supplierRepository.GetAllAsync();
    }
}