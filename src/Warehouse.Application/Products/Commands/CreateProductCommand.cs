namespace Warehouse.Application.Products.Commands;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record CreateProductCommand(
    string Name, 
    string Sku, 
    string Description, 
    decimal Price, 
    int QuantityInStock, 
    DateTime? ExpiryDate
) : IRequest<Guid>;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;

    public CreateProductCommandHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Enforce domain validation rules on creation
        var product = Product.Create(request.Name, request.Sku, request.Price, request.QuantityInStock);
        product.Description = request.Description;
        product.ExpiryDate = request.ExpiryDate;

        await _productRepository.AddAsync(product);
        return product.Id;
    }
}