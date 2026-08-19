using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Shipments;
using Warehouse.Application.Shipments.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Application.Shipments.Queries
{
    public record GetShipmentStatusQuery(Guid Id) : IRequest<ShipmentStatusViewModel?>;
}

namespace Warehouse.Infrastructure.Queries
{
    public class GetShipmentStatusQueryHandler : IRequestHandler<GetShipmentStatusQuery, ShipmentStatusViewModel?>
    {
        private readonly WarehouseDbContext _context;

        public GetShipmentStatusQueryHandler(WarehouseDbContext context)
        {
            _context = context;
        }

        public Task<ShipmentStatusViewModel?> Handle(
            GetShipmentStatusQuery request,
            CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
