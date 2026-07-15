namespace Warehouse.Application.Products.Queries;

using MediatR;
using System.Collections.Generic;

public record ExpiryYearAndCountryGroupDto(int Year, string Country, int TotalProducts, IEnumerable<ProductViewModel> Products);

public record GetProductsGroupedByExpiryAndCountryQuery() : IRequest<IEnumerable<ExpiryYearAndCountryGroupDto>>;
