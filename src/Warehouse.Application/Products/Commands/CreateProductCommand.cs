namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

public record CreateProductCommand(
    string Name,
    string Sku,
    string Description,
    decimal Price,
    int QuantityInStock,
    DateTime? ExpiryDate
) : IRequest<Result<ProductViewModel>>;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductViewModel>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public CreateProductCommandHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<Result<ProductViewModel>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        Product product;
        try
        {
            // Enforce domain validation rules on creation
            product = Product.Create(request.Name, request.Sku, request.Price, request.QuantityInStock);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ProductViewModel>(ErrorType.Validation, ex.Message);
        }

        product.Description = request.Description;
        product.ExpiryDate = request.ExpiryDate;

        await _productRepository.AddAsync(product, cancellationToken);
        return Result.Success(_mapper.Map<ProductViewModel>(product));
    }
}
