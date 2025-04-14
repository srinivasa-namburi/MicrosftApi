using Orleans;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events
{
    [GenerateSerializer(
        GenerateFieldIds = GenerateFieldIds.PublicProperties, 
        IncludePrimaryConstructorParameters = true)]
    public record ReviewCompletedNotification(Guid CorrelationId);
}