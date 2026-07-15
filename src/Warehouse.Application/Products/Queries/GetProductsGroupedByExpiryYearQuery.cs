namespace Warehouse.Application.Products.Queries;

using MediatR;
using System.Collections.Generic;

public record ExpiryYearGroupDto(int Year, int TotalProducts, IEnumerable<ProductViewModel> Products);

public record GetProductsGroupedByExpiryYearQuery() : IRequest<IEnumerable<ExpiryYearGroupDto>>;
