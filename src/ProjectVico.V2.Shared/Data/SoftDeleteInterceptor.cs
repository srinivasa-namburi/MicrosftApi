using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Data;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new CancellationToken())
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

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, 
        InterceptionResult<int> result)
    {
        // Call the Async method to avoid code duplication
        return SavingChangesAsync(eventData, result).Result;

    }
}