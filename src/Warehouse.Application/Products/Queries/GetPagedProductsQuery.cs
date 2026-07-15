using MediatR;

namespace Warehouse.Application.Products.Queries;

public record PagedResponseDto<T>(IEnumerable<T> Items, int PageNumber, int PageSize, int TotalCount);

public record GetPagedProductsQuery(int PageNumber = 1, int PageSize = 10)
    : IRequest<PagedResponseDto<ProductViewModel>>;
