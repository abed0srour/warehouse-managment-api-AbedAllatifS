using MediatR;

namespace Warehouse.Application.Products.Queries;

public record GetTotalProductCountQuery() : IRequest<int>;