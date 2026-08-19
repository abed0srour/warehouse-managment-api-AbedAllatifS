using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Shipments;
using Warehouse.Application.Shipments.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Application.Shipments.Queries
{
    public record GetShipmentByIdQuery(Guid Id) : IRequest<ShipmentViewModel?>;
}

namespace Warehouse.Infrastructure.Queries
{
    public class GetShipmentByIdQueryHandler : IRequestHandler<GetShipmentByIdQuery, ShipmentViewModel?>
    {
        private readonly WarehouseDbContext _context;

        public GetShipmentByIdQueryHandler(WarehouseDbContext context)
        {
            _context = context;
        }

        public Task<ShipmentViewModel?> Handle(
            GetShipmentByIdQuery request,
            CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
