namespace Warehouse.Application.Products.Queries;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record GetProductByIdQuery(Guid Id) : IRequest<ProductViewModel?>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductViewModel?>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public GetProductByIdQueryHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<ProductViewModel?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        return product == null ? null : _mapper.Map<ProductViewModel>(product);
    }
}
