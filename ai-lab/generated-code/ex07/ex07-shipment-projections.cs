using System;
using System.Linq.Expressions;
using Warehouse.Application.Shipments;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

internal static class ShipmentProjections
{
    public static readonly Expression<Func<Shipment, ShipmentViewModel>> ToViewModel = null!;

    public static readonly Expression<Func<Shipment, ShipmentStatusViewModel>> ToStatusViewModel = null!;
}
