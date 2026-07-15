namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record ArchiveProductCommand(Guid Id) : IRequest<Result<ProductViewModel>>;

public class ArchiveProductCommandHandler : IRequestHandler<ArchiveProductCommand, Result<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public ArchiveProductCommandHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<Result<ProductViewModel>> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductViewModel>(ErrorType.NotFound, $"Product with ID {request.Id} was not found.");
        }

        product.Archive();
        await _productRepository.UpdateAsync(product, cancellationToken);
        return Result.Success(_mapper.Map<ProductViewModel>(product));
    }
}
