namespace Warehouse.Notifications.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
        public required string Message { get; set; }
        public required string Severity { get; set; }
        public NotificationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public required string RelatedEntityId { get; set; }
        public required string RelatedEntityType { get; set; }
        public string? EventId { get; set; }

        public void MarkAsRead()
        {
            Status = NotificationStatus.Read;
        }
    }
}
