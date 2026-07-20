namespace Warehouse.Application.Products.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common.Exceptions;
using Warehouse.Domain;

public record CreateProductCommand(
    string Name,
    string Sku,
    string Description,
    decimal Price,
    int QuantityInStock,
    string? SupplierName,
    DateTime? ExpiryDate
) : IRequest<Product>;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Product>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public CreateProductCommandHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<Product> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var existingProducts = await _productRepository.GetAllAsync(cancellationToken);
        var duplicateSku = existingProducts.Any(p =>
            string.Equals(p.Sku, request.Sku, StringComparison.OrdinalIgnoreCase));

        if (duplicateSku)
        {
            throw new BusinessRuleException($"A product with SKU '{request.Sku}' already exists.");
        }

        // Enforce domain validation rules on creation
        var product = Product.Create(request.Name, request.Sku, request.Price, request.QuantityInStock);
        product.Description = request.Description;
        product.SupplierName = request.SupplierName;
        product.ExpiryDate = request.ExpiryDate;

        await _productRepository.AddAsync(product, cancellationToken);
        return product;
    }
}
