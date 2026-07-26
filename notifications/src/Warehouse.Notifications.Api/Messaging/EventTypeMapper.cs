using Warehouse.Domain.Events;

namespace Warehouse.Notifications.Api.Messaging
{
    public static class EventTypeMapper
    {
        public static string ToNotificationType(string routingKey) => routingKey switch
        {
            "stock.low" => "StockLow",
            "file.uploaded" => "FileUploaded",
            _ => throw new NotSupportedException($"No notification mapping for routing key '{routingKey}'.")
        };

        public static string ToTitle(string routingKey) => routingKey switch
        {
            "stock.low" => "Low stock alert",
            "file.uploaded" => "File uploaded",
            _ => throw new NotSupportedException($"No notification mapping for routing key '{routingKey}'.")
        };

        public static string ToMessage(string routingKey, IntegrationEvent @event) => routingKey switch
        {
            "stock.low" when @event is StockLowDetected e =>
                $"{e.ProductName} has only {e.CurrentQuantity} units left (threshold: {e.Threshold}).",
            "file.uploaded" when @event is WarehouseFileUploaded e =>
                $"{e.FileName} was uploaded by {e.UploadedByUserId}.",
            _ => throw new NotSupportedException($"No message template for routing key '{routingKey}'.")
        };
    }
}
