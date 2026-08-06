namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

/// <summary>
/// Receives or issues stock against a product. <paramref name="QuantityChange"/> is signed:
/// positive increases the level, negative decreases it. The caller-facing contract splits that
/// into a type and a magnitude; the translation happens at the controller boundary so the
/// command stays a single relative delta.
/// </summary>
public record AdjustStockCommand(
    Guid ProductId,
    int QuantityChange,
    string? Reason = null) : IRequest<Result<ProductViewModel>>;

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AdjustStockCommandHandler> _logger;

    public AdjustStockCommandHandler(
        IProductRepository productRepository,
        IMapper mapper,
        IDistributedCache cache,
        ILogger<AdjustStockCommandHandler> logger)
    {
        _productRepository = productRepository;
        _mapper = mapper;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<ProductViewModel>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductViewModel>(
                ErrorType.NotFound, $"Product with ID {request.ProductId} was not found.");
        }

        var quantityBefore = product.QuantityInStock;

        try
        {
            product.AdjustStock(request.QuantityChange);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ProductViewModel>(ErrorType.Validation, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Archived product, or a decrease that would take the level below zero.
            return Result.Failure<ProductViewModel>(ErrorType.Conflict, ex.Message);
        }

        await _productRepository.UpdateAsync(product, cancellationToken);

        await _cache.RemoveAsync("products:all:True", cancellationToken);
        await _cache.RemoveAsync("products:all:False", cancellationToken);
        await _cache.RemoveAsync($"products:{request.ProductId}", cancellationToken);

        // The reason is the audit trail for now. Newlines are stripped because it is
        // caller-supplied text landing in a plain-text log sink.
        _logger.LogInformation(
            "Stock for product {ProductId} adjusted by {QuantityChange} from {QuantityBefore} to {QuantityAfter}. Reason: {Reason}",
            product.Id,
            request.QuantityChange,
            quantityBefore,
            product.QuantityInStock,
            Sanitise(request.Reason));

        return Result.Success(_mapper.Map<ProductViewModel>(product));
    }

    private static string Sanitise(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? "(none given)"
            : reason.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
}
