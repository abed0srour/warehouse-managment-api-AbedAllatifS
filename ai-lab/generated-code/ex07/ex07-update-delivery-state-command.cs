namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record UpdateDeliveryStateCommand(
    Guid ShipmentId,
    string NewStatus,
    string? TrackingNumber = null,
    string? Note = null) : IRequest<Result<ShipmentViewModel>>;

public class UpdateDeliveryStateCommandHandler
    : IRequestHandler<UpdateDeliveryStateCommand, Result<ShipmentViewModel>>
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IPublisher _publisher;
    private readonly IMapper _mapper;

    public UpdateDeliveryStateCommandHandler(
        IShipmentRepository shipmentRepository,
        IPublisher publisher,
        IMapper mapper)
    {
        _shipmentRepository = shipmentRepository;
        _publisher = publisher;
        _mapper = mapper;
    }

    public Task<Result<ShipmentViewModel>> Handle(
        UpdateDeliveryStateCommand request,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}
