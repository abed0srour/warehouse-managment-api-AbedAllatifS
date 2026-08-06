using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Shipments;
using Warehouse.Application.Shipments.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

public class GetShipmentByIdQueryHandler : IRequestHandler<GetShipmentByIdQuery, ShipmentViewModel?>
{
    private readonly WarehouseDbContext _context;

    public GetShipmentByIdQueryHandler(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<ShipmentViewModel?> Handle(GetShipmentByIdQuery request, CancellationToken cancellationToken)
    {
        // The lines come back inside the projection, so this is a single round trip -- no
        // Include, no lazy loading, no per-line query.
        return await _context.Shipments
            .AsNoTracking()
            .Where(s => s.Id == request.Id)
            .Select(ShipmentProjections.ToViewModel)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
