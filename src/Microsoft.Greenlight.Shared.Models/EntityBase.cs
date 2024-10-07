using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Models;

public abstract class EntityBase
{
    protected EntityBase()
    {
        Id = Guid.NewGuid();
    }

    protected EntityBase(Guid id)
    {
        Id = id;
    }

    [Key]
    public Guid Id { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    //public bool IsActive { get; set; } = true;
    //public DateTimeOffset? DeletedAt { get; set; }

    //public void Undo()
    //{
    //    IsActive = true;
    //    DeletedAt = null;
    //}

    //public void Delete()
    //{
    //    IsActive = false;
    //    DeletedAt = DateTimeOffset.UtcNow;
    //}
}
