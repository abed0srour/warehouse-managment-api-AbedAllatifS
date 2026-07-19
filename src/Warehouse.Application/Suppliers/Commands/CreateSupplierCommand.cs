namespace Warehouse.Application.Suppliers.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record CreateSupplierCommand(string Name, string Country, string ContactEmail, string PhoneNumber) : IRequest<Supplier>;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Supplier>
{
    private readonly ISupplierRepository _supplierRepository;

    public CreateSupplierCommandHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<Supplier> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Supplier name is required.");
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
        return supplier;
    }
}
