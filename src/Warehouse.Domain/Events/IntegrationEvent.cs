using System;

namespace Warehouse.Domain.Events
{
    public abstract class IntegrationEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public string RelatedEntityId { get; set; } = string.Empty;
        public string RelatedEntityType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }
}