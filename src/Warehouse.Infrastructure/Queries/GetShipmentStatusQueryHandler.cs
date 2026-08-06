using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Shipments;
using Warehouse.Application.Shipments.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

public class GetShipmentStatusQueryHandler : IRequestHandler<GetShipmentStatusQuery, ShipmentStatusViewModel?>
{
    private readonly WarehouseDbContext _context;

    public GetShipmentStatusQueryHandler(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<ShipmentStatusViewModel?> Handle(GetShipmentStatusQuery request, CancellationToken cancellationToken)
    {
        return await _context.Shipments
            .AsNoTracking()
            .Where(s => s.Id == request.Id)
            .Select(ShipmentProjections.ToStatusViewModel)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
