namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record NotifySupplierCommand(Guid ShipmentId, string? Message = null)
    : IRequest<Result<ShipmentViewModel>>;

public class NotifySupplierCommandHandler : IRequestHandler<NotifySupplierCommand, Result<ShipmentViewModel>>
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierNotificationService _notificationService;
    private readonly IMapper _mapper;

    public NotifySupplierCommandHandler(
        IShipmentRepository shipmentRepository,
        ISupplierRepository supplierRepository,
        ISupplierNotificationService notificationService,
        IMapper mapper)
    {
        _shipmentRepository = shipmentRepository;
        _supplierRepository = supplierRepository;
        _notificationService = notificationService;
        _mapper = mapper;
    }

    public Task<Result<ShipmentViewModel>> Handle(
        NotifySupplierCommand request,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    private static string DefaultMessage(Shipment shipment) => throw new NotImplementedException();
}
