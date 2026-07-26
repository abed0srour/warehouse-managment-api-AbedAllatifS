using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Warehouse.Notifications.Api.Data;
using Warehouse.Notifications.Api.Entities;

namespace Warehouse.Notifications.Api.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly NotificationsDbContext _dbContext;

        public NotificationRepository(NotificationsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Notification>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.Notifications
                .AsNoTracking()
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        }

        public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken)
        {
            _dbContext.Notifications.Update(notification);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}