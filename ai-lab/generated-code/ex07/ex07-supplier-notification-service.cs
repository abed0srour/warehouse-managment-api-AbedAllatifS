namespace Warehouse.Infrastructure.Notifications;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Warehouse.Domain;

public class LoggingSupplierNotificationService : ISupplierNotificationService
{
    private readonly ILogger<LoggingSupplierNotificationService> _logger;

    public LoggingSupplierNotificationService(ILogger<LoggingSupplierNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifySupplierAsync(
        SupplierNotification notification,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
