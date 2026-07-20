namespace Warehouse.Application.Suppliers.Queries;

using AutoMapper;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetAllSuppliersQuery() : IRequest<IEnumerable<SupplierViewModel>>;

public class GetAllSuppliersQueryHandler : IRequestHandler<GetAllSuppliersQuery, IEnumerable<SupplierViewModel>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IMapper _mapper;

    public GetAllSuppliersQueryHandler(ISupplierRepository supplierRepository, IMapper mapper)
    {
        _supplierRepository = supplierRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SupplierViewModel>> Handle(GetAllSuppliersQuery request, CancellationToken cancellationToken)
    {
        return await _supplierRepository.GetAllAsync(cancellationToken);
    }
}
