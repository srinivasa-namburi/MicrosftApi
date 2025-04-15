using Orleans;

public interface IContentReferenceIndexingGrain : IGrainWithGuidKey
{
    Task ExecuteAsync();
}