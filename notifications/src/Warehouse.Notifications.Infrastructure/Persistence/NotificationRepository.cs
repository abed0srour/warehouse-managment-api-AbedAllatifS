using Microsoft.EntityFrameworkCore;
using Warehouse.Notifications.Domain.Entities;
using Warehouse.Notifications.Domain.Repositories;

namespace Warehouse.Notifications.Infrastructure.Persistence
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

        public async Task<bool> ExistsByEventIdAsync(string eventId, CancellationToken cancellationToken)
        {
            return await _dbContext.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.EventId == eventId, cancellationToken);
        }

        public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
        {
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
