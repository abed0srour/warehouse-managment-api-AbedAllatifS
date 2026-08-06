using MediatR;

namespace Warehouse.Application.Products.Queries;

public record GetOutOfStockProductsQuery : IRequest<IEnumerable<OutOfStockProductDto>>;
