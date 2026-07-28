using Warehouse.Notifications.Domain.Entities;

namespace Warehouse.Notifications.Domain.Repositories
{
    public interface INotificationRepository
    {
        Task<IEnumerable<Notification>> GetAllAsync(CancellationToken cancellationToken);
        Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task UpdateAsync(Notification notification, CancellationToken cancellationToken);
        Task<bool> ExistsByEventIdAsync(string eventId, CancellationToken cancellationToken);
        Task AddAsync(Notification notification, CancellationToken cancellationToken);
    }
}
