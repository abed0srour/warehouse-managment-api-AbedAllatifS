namespace Warehouse.Notifications.Infrastructure.Configuration
{
    public class NotificationPreferencesOptions : Dictionary<string, NotificationPreferenceOptionsEntry>
    {
        public const string SectionName = "NotificationPreferences";
    }

    public class NotificationPreferenceOptionsEntry
    {
        public string? Severity { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
