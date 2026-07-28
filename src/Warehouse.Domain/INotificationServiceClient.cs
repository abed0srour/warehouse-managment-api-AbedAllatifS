namespace Warehouse.Domain
{
    public interface INotificationServiceClient
    {
        // Returns null if the Notification Service is unreachable, times out, or errors —
        // callers must treat null as "unavailable", not as zero.
        Task<int?> GetUnreadNotificationCountAsync(CancellationToken cancellationToken);
    }
}
