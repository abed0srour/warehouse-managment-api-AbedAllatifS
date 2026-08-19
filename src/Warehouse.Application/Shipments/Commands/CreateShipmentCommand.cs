namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

/// <summary>
/// Opens a shipment against a supplier. Header only -- products arrive through
/// <c>AssignProductsToShipmentCommand</c>, which keeps this handler free of stock rules.
/// Leave <paramref name="ReferenceNumber"/> blank and the handler assigns one.
/// </summary>
public record CreateShipmentCommand(
    Guid SupplierId,
    string DestinationLine1,
    string DestinationCity,
    string DestinationPostalCode,
    string DestinationCountry,
    DateTime? ExpectedDeliveryDate,
    string? ReferenceNumber = null) : IRequest<Result<ShipmentViewModel>>;

public class CreateShipmentCommandHandler : IRequestHandler<CreateShipmentCommand, Result<ShipmentViewModel>>
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IMapper _mapper;

    public CreateShipmentCommandHandler(
        IShipmentRepository shipmentRepository,
        ISupplierRepository supplierRepository,
        IMapper mapper)
    {
        _shipmentRepository = shipmentRepository;
        _supplierRepository = supplierRepository;
        _mapper = mapper;
    }

    public async Task<Result<ShipmentViewModel>> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.NotFound, $"Supplier with ID {request.SupplierId} was not found.");
        }

        var reference = string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? GenerateReferenceNumber()
            : request.ReferenceNumber.Trim();

        // Targeted lookup rather than scanning every shipment, so this stays O(1) as the table grows.
        var existing = await _shipmentRepository.GetByReferenceNumberAsync(reference, cancellationToken);
        if (existing != null)
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.Conflict, $"A shipment with reference '{reference}' already exists.");
        }

        Shipment shipment;
        try
        {
            var destination = Address.Create(
                request.DestinationLine1,
                request.DestinationCity,
                request.DestinationPostalCode,
                request.DestinationCountry);

            shipment = Shipment.Create(reference, supplier, destination, request.ExpectedDeliveryDate);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ShipmentViewModel>(ErrorType.Validation, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Raised when the supplier is inactive.
            return Result.Failure<ShipmentViewModel>(ErrorType.Conflict, ex.Message);
        }

        await _shipmentRepository.AddAsync(shipment, cancellationToken);

        return Result.Success(_mapper.Map<ShipmentViewModel>(shipment));
    }

    private static string GenerateReferenceNumber() =>
        $"SHP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
