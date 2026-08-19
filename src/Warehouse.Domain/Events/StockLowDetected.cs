namespace Warehouse.Domain.Events
{
    public class StockLowDetected : IntegrationEvent
    {
        public StockLowDetected(Guid productId, string productName, int currentQuantity, int threshold)
        {
            EventType = "stock.low";
            Severity = "Warning";
            RelatedEntityType = "Product";
            RelatedEntityId = productId.ToString();

            ProductId = productId;
            ProductName = productName;
            CurrentQuantity = currentQuantity;
            Threshold = threshold;
        }

        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentQuantity { get; set; }
        public int Threshold { get; set; }
    }
}