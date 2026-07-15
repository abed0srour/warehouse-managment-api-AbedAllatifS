namespace Warehouse.Application.Products.Queries;

using MediatR;
using System.Collections.Generic;

public record GetProductsBySupplierQuery(string SupplierName, string SortOrder = "desc")
    : IRequest<IEnumerable<ProductViewModel>>;
