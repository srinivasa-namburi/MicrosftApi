using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.ContentReference;

public interface IContentReferenceVectorRepository
{
    Task IndexAsync(ContentReferenceItem reference, CancellationToken ct = default);
    Task ReindexAllAsync(ContentReferenceType type, CancellationToken ct = default);
    Task DeleteAsync(Guid referenceId, ContentReferenceType type, CancellationToken ct = default);
}

