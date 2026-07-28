namespace Warehouse.Notifications.Application.EventProcessing
{
    public enum ProcessEventResult
    {
        Created,
        SkippedDuplicate,
        SkippedByPreference
    }
}
