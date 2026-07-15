namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record UpdateProductPriceCommand(Guid Id, decimal NewPrice) : IRequest<Result<ProductViewModel>>;

public class UpdateProductPriceCommandHandler : IRequestHandler<UpdateProductPriceCommand, Result<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public UpdateProductPriceCommandHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<Result<ProductViewModel>> Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductViewModel>(ErrorType.NotFound, $"Product with ID {request.Id} was not found.");
        }

        try
        {
            product.UpdatePrice(request.NewPrice);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ProductViewModel>(ErrorType.Conflict, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ProductViewModel>(ErrorType.Validation, ex.Message);
        }

        await _productRepository.UpdateAsync(product, cancellationToken);
        return Result.Success(_mapper.Map<ProductViewModel>(product));
    }
}
