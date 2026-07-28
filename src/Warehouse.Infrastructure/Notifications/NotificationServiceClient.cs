using System.Text.Json;
using Microsoft.Extensions.Logging;
using Warehouse.Domain;

namespace Warehouse.Infrastructure.Notifications
{
    public class NotificationServiceClient : INotificationServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NotificationServiceClient> _logger;

        public NotificationServiceClient(HttpClient httpClient, ILogger<NotificationServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<int?> GetUnreadNotificationCountAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync("/api/notifications?status=Unread", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Notification Service returned {StatusCode} while fetching unread notification count.",
                        response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                return document.RootElement.GetArrayLength();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // The Notification Service being down/slow/erroring must never bubble up
                // into a 500 on the warehouse API — callers get "unavailable" instead.
                _logger.LogWarning(ex, "Failed to reach the Notification Service for unread notification count.");
                return null;
            }
        }
    }
}
