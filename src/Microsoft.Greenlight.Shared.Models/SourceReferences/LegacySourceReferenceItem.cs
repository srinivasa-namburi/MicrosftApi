using System.Text.Json;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Legacy placeholder type for rows persisted with the base discriminator value "SourceReferenceItem".
/// This type carries only the base properties and prevents EF from failing to materialize abstract base types.
/// New code should not create instances of this type; it exists only to load historical data safely.
/// </summary>
public sealed class LegacySourceReferenceItem : SourceReferenceItem
{
    /// <summary>
    /// Stores optional serialized output when present in legacy rows.
    /// </summary>
    public override string? SourceOutput { get; set; }

    /// <inheritdoc />
    public override void SetBasicParameters()
    {
        // Use a very generic classification; callers typically ignore legacy items in generation flows.
        SourceReferenceType = Enums.SourceReferenceType.GeneralKnowledge;
        Description = "Legacy source reference item (base type).";
    }
}
