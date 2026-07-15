namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record UpdateProductQuantityCommand(Guid Id, int NewQuantity) : IRequest<Result<ProductViewModel>>;

public class UpdateProductQuantityCommandHandler : IRequestHandler<UpdateProductQuantityCommand, Result<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public UpdateProductQuantityCommandHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<Result<ProductViewModel>> Handle(UpdateProductQuantityCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductViewModel>(ErrorType.NotFound, $"Product with ID {request.Id} was not found.");
        }

        try
        {
            product.UpdateQuantity(request.NewQuantity);
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
