namespace Warehouse.Domain;

using System;

public class WarehouseFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string UploadedByUid { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = string.Empty;
    public int RelatedEntityId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
