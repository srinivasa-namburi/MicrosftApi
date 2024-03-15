using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Data;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, 
        InterceptionResult<int> result)
    {
        if (eventData.Context is null) return result;
        
        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            if (entry is not { State: EntityState.Deleted, Entity: EntityBase deleteEntity }) continue;
            entry.State = EntityState.Modified;
            deleteEntity.IsActive = false;
            deleteEntity.DeletedAt = DateTimeOffset.UtcNow;
        }
        return result;
    }
}