using Orleans;

public interface IRepositoryIndexMaintenanceGrain : IGrainWithGuidKey
{
    Task ExecuteAsync();
}