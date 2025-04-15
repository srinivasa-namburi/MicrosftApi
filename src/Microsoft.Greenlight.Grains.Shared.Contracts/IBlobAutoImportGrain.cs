using Orleans;

public interface IBlobAutoImportGrain : IGrainWithGuidKey 
{
    Task ExecuteAsync();
}