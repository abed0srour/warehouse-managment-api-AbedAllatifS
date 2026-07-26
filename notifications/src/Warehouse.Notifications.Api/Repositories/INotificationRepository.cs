using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Notifications.Api.Entities;

namespace Warehouse.Notifications.Api.Repositories
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