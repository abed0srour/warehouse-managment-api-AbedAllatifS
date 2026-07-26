namespace Warehouse.Notifications.Api.Preferences
{
    // Keyed by notification type (matches EventTypeMapper.ToNotificationType, e.g. "StockLow", "FileUploaded").
    public class NotificationPreferencesOptions : Dictionary<string, NotificationPreferenceEntry>
    {
        public const string SectionName = "NotificationPreferences";
    }

    public class NotificationPreferenceEntry
    {
        public string? Severity { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
