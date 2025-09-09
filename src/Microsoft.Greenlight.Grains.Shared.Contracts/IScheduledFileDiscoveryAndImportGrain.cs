using Orleans;

public interface IScheduledFileDiscoveryAndImportGrain : IGrainWithGuidKey 
{
    Task ExecuteAsync();
}