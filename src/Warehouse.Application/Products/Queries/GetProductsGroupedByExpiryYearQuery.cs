namespace Warehouse.Application.Products.Queries;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;
public record ExpiryYearGroupDto(int Year, int TotalProducts, IEnumerable<object> Products);

public record GetProductsGroupedByExpiryYearQuery() : IRequest<IEnumerable<ExpiryYearGroupDto>>;