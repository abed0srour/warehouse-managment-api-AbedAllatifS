namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record ShipmentProductAssignment(Guid ProductId, int Quantity);

/// <summary>
/// Adds products to a draft shipment. Every id in the batch is resolved before any line is
/// applied, and nothing is persisted unless all of them pass the domain rules, so a request
/// referencing one bad product cannot leave the shipment half-filled.
/// </summary>
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

    public async Task<Result<ShipmentViewModel>> Handle(AssignProductsToShipmentCommand request, CancellationToken cancellationToken)
    {
        if (request.Products == null || request.Products.Count == 0)
        {
            return Result.Failure<ShipmentViewModel>(ErrorType.Validation, "At least one product must be supplied.");
        }

        var shipment = await _shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment == null)
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.NotFound, $"Shipment with ID {request.ShipmentId} was not found.");
        }

        // One query for the whole batch instead of one per line.
        var products = await _productRepository.GetByIdsAsync(
            request.Products.Select(p => p.ProductId), cancellationToken);

        var productsById = products.ToDictionary(p => p.Id);

        var missing = request.Products
            .Select(p => p.ProductId)
            .Distinct()
            .Where(id => !productsById.ContainsKey(id))
            .ToList();

        if (missing.Count > 0)
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.NotFound, $"Products not found: {string.Join(", ", missing)}.");
        }

        try
        {
            foreach (var assignment in request.Products)
            {
                shipment.AssignProduct(productsById[assignment.ProductId], assignment.Quantity);
            }
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ShipmentViewModel>(ErrorType.Validation, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Raised for a non-draft shipment, an archived product, or insufficient stock.
            return Result.Failure<ShipmentViewModel>(ErrorType.Conflict, ex.Message);
        }

        await _shipmentRepository.UpdateAsync(shipment, cancellationToken);

        return Result.Success(_mapper.Map<ShipmentViewModel>(shipment));
    }
}
