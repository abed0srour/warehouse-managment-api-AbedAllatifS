namespace Warehouse.Domain.Events
{
    public class WarehouseFileUploaded : IntegrationEvent
    {
        public WarehouseFileUploaded(Guid fileId, string fileName, string uploadedByUserId, string contentType)
        {
            EventType = "file.uploaded";
            Severity = "Info";
            RelatedEntityType = "WarehouseFile";
            RelatedEntityId = fileId.ToString();

            FileId = fileId;
            FileName = fileName;
            UploadedByUserId = uploadedByUserId;
            ContentType = contentType;
        }

        public Guid FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string UploadedByUserId { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}