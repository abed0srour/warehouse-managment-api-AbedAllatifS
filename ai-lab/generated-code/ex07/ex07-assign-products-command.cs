namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record ShipmentProductAssignment(Guid ProductId, int Quantity);

public record AssignProductsToShipmentCommand(
    Guid ShipmentId,
    IReadOnlyList<ShipmentProductAssignment> Products) : IRequest<Result<ShipmentViewModel>>;

public class AssignProductsToShipmentCommandHandler
    : IRequestHandler<AssignProductsToShipmentCommand, Result<ShipmentViewModel>>
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public AssignProductsToShipmentCommandHandler(
        IShipmentRepository shipmentRepository,
        IProductRepository productRepository,
        IMapper mapper)
    {
        _shipmentRepository = shipmentRepository;
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public Task<Result<ShipmentViewModel>> Handle(
        AssignProductsToShipmentCommand request,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}

public interface IProductRepositoryAddition
{
    Task<IReadOnlyList<Product>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);
}
