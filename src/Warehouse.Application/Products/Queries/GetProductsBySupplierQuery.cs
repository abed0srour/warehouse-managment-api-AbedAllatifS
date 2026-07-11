namespace Warehouse.Application.Products.Queries;

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;


public record GetProductsBySupplierQuery(string SupplierName, string SortOrder = "desc") 
    : IRequest<IEnumerable<object>>;