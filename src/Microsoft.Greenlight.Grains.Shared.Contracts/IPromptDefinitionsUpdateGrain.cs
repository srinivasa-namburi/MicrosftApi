using Orleans;

public interface IPromptDefinitionsUpdateGrain : IGrainWithGuidKey
{
    Task ExecuteAsync();
}