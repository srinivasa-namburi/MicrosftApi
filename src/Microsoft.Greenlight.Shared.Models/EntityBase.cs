using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents the base class for all entities with common properties.
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityBase"/> class with a new GUID.
    /// </summary>
    protected EntityBase()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityBase"/> class with the specified GUID.
    /// </summary>
    /// <param name="id">The unique identifier for the entity.</param>
    protected EntityBase(Guid id)
    {
        Id = id;
    }

    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    [Key]
    public virtual Guid Id { get; set; }

    /// <summary>
    /// Row version for concurrency control.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// UTC date and time when the entity was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC date and time when the entity was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}
