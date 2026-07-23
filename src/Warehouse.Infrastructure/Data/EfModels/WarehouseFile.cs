using System;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class WarehouseFile
{
    public Guid Id { get; set; }

    public string ObjectKey { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public string UploadedByUid { get; set; } = null!;

    public string RelatedEntityType { get; set; } = null!;

    public int RelatedEntityId { get; set; }

    public DateTime UploadedAtUtc { get; set; }
}
