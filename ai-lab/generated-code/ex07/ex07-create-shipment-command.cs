namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

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

    public Task<Result<ShipmentViewModel>> Handle(
        CreateShipmentCommand request,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    private static string GenerateReferenceNumber() => throw new NotImplementedException();
}
