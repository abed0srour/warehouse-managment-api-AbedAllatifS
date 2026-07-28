namespace Warehouse.Notifications.Domain.Preferences
{
    public interface INotificationPreferenceProvider
    {
        NotificationPreference? GetPreference(string notificationType);
    }
}
