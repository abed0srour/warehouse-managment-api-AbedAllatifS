
namespace Warehouse.Application.Products.Queries;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;
public record ExpiryYearAndCountryGroupDto(int Year, string Country, int TotalProducts, IEnumerable<object> Products);

public record GetProductsGroupedByExpiryAndCountryQuery() : IRequest<IEnumerable<ExpiryYearAndCountryGroupDto>>;