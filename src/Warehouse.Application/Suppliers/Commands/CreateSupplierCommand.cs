namespace Warehouse.Application.Suppliers.Commands;

using AutoMapper;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record CreateSupplierCommand(string Name, string Country, string ContactEmail, string PhoneNumber) : IRequest<Result<SupplierViewModel>>;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Result<SupplierViewModel>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IMapper _mapper;

    public CreateSupplierCommandHandler(ISupplierRepository supplierRepository, IMapper mapper)
    {
        _supplierRepository = supplierRepository;
        _mapper = mapper;
    }

    public async Task<Result<SupplierViewModel>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<SupplierViewModel>(ErrorType.Validation, "Supplier name is required.");
        }

        var supplier = new Supplier
        {
            Name = request.Name,
            Country = request.Country,
            ContactEmail = request.ContactEmail,
            PhoneNumber = request.PhoneNumber,
            IsActive = true
        };

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        return Result.Success(_mapper.Map<SupplierViewModel>(supplier));
    }
}
