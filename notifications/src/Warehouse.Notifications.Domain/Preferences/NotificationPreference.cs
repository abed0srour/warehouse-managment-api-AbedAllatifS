namespace Warehouse.Notifications.Domain.Preferences
{
    public class NotificationPreference
    {
        public string? Severity { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
