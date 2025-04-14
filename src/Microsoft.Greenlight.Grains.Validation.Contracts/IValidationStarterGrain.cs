using Orleans;

namespace Microsoft.Greenlight.Grains.Validation.Contracts
{
    public interface IValidationStarterGrain : IGrainWithGuidKey
    {
        Task<Guid> StartValidationForDocumentAsync(Guid generatedDocumentId);
    }
}