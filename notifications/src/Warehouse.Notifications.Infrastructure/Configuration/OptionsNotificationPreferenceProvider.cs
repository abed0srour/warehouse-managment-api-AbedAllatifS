using Microsoft.Extensions.Options;
using Warehouse.Notifications.Domain.Preferences;

namespace Warehouse.Notifications.Infrastructure.Configuration
{
    public class OptionsNotificationPreferenceProvider : INotificationPreferenceProvider
    {
        private readonly NotificationPreferencesOptions _options;

        public OptionsNotificationPreferenceProvider(IOptions<NotificationPreferencesOptions> options)
        {
            _options = options.Value;
        }

        public NotificationPreference? GetPreference(string notificationType)
        {
            if (!_options.TryGetValue(notificationType, out var entry))
            {
                return null;
            }

            return new NotificationPreference
            {
                Severity = entry.Severity,
                Enabled = entry.Enabled
            };
        }
    }
}
