using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.State;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared;

[Reentrant]
public class ContentReferenceReindexOrchestrationGrain : Grain, IContentReferenceReindexOrchestrationGrain
{
    private readonly ILogger<ContentReferenceReindexOrchestrationGrain> _logger;
    private readonly IPersistentState<ContentReferenceReindexState> _state;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbFactory;
    private readonly IContentReferenceVectorRepository _vectorRepo;

    private volatile bool _running = false;

    public ContentReferenceReindexOrchestrationGrain(
        [PersistentState("crReindexOrchestration")] IPersistentState<ContentReferenceReindexState> state,
        ILogger<ContentReferenceReindexOrchestrationGrain> logger,
        IDbContextFactory<DocGenerationDbContext> dbFactory,
        IContentReferenceVectorRepository vectorRepo)
    {
        _state = state;
        _logger = logger;
        _dbFactory = dbFactory;
        _vectorRepo = vectorRepo;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_state.State.Id))
        {
            _state.State.Id = this.GetPrimaryKeyString();
            await _state.WriteStateAsync();
        }
        _running = _state.State.Running;
        await base.OnActivateAsync(cancellationToken);
    }

    public Task<ContentReferenceReindexState> GetStateAsync()
        => Task.FromResult(_state.State);

    public Task<bool> IsRunningAsync() => Task.FromResult(_running);

    public async Task StartReindexingAsync(ContentReferenceType type, string reason)
    {
        if (_running)
        {
            _logger.LogInformation("Content reference reindex already running for key {Key}", _state.State.Id);
            return;
        }

        _running = true;
        _state.State.ReferenceType = type;
        _state.State.Reason = reason;
        _state.State.StartedUtc = DateTime.UtcNow;
        _state.State.CompletedUtc = null;
        _state.State.Processed = 0;
        _state.State.Failed = 0;
        _state.State.Errors.Clear();
        _state.State.Running = true;
        await _state.WriteStateAsync();

        _ = Task.Run(async () =>
        {
            ConcurrencyLease? lease = null;
            try
            {
                // Signal start to interested clients + system status pipeline
                var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await notifier.NotifyContentReferenceReindexStartedAsync(new ContentReferenceReindexStartedNotification(
                    _state.State.Id,
                    _state.State.ReferenceType,
                    reason));

                // Enumerate references of this type
                await using var db = await _dbFactory.CreateDbContextAsync();
                var list = await db.ContentReferenceItems
                    .Where(r => r.ReferenceType == type)
                    .ToListAsync();
                _state.State.Total = list.Count;
                // Initialize per-source totals
                _state.State.Sources = list
                    .GroupBy(r => r.ContentReferenceSourceId?.ToString() ?? string.Empty)
                    .Select(g => new ContentReferenceReindexSourceProgress
                    {
                        SourceId = string.IsNullOrEmpty(g.Key) ? null : g.Key,
                        Total = g.Count(),
                        Processed = 0,
                        Failed = 0
                    })
                    .ToList();
                await _state.WriteStateAsync();

                foreach (var r in list)
                {
                    ConcurrencyLease? itemLease = null;
                    try
                    {
                        // Gate parallelization via global ingestion coordinator per item
                        var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(ConcurrencyCategory.Ingestion.ToString());
                        var reqId = $"CRReindexItem:{_state.State.ReferenceType}:{r.Id}";
                        itemLease = await coordinator.AcquireAsync(reqId, weight: 1, waitTimeout: TimeSpan.FromDays(2), leaseTtl: TimeSpan.FromMinutes(15));

                        await _vectorRepo.IndexAsync(r);
                        _state.State.Processed++;
                        var sid = r.ContentReferenceSourceId?.ToString() ?? string.Empty;
                        var src = _state.State.Sources.FirstOrDefault(s => (s.SourceId ?? string.Empty) == sid);
                        if (src != null) { src.Processed++; }

                        // Throttled progress events (every 10 items or at milestones)
                        if (_state.State.Processed % 10 == 0 || _state.State.Processed == _state.State.Total)
                        {
                            await notifier.NotifyContentReferenceReindexProgressAsync(new ContentReferenceReindexProgressNotification(
                                _state.State.Id,
                                _state.State.ReferenceType,
                                _state.State.Total,
                                _state.State.Processed,
                                _state.State.Failed));
                        }
                    }
                    catch (Exception ex)
                    {
                        _state.State.Failed++;
                        _state.State.Errors.Add($"{r.Id}: {ex.Message}");
                        var sid = r.ContentReferenceSourceId?.ToString() ?? string.Empty;
                        var src = _state.State.Sources.FirstOrDefault(s => (s.SourceId ?? string.Empty) == sid);
                        if (src != null) { src.Failed++; }

                        await notifier.NotifyContentReferenceReindexProgressAsync(new ContentReferenceReindexProgressNotification(
                            _state.State.Id,
                            _state.State.ReferenceType,
                            _state.State.Total,
                            _state.State.Processed,
                            _state.State.Failed));
                    }
                    finally
                    {
                        if (itemLease != null)
                        {
                            try
                            {
                                var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(ConcurrencyCategory.Ingestion.ToString());
                                await coordinator.ReleaseAsync(itemLease.LeaseId);
                            }
                            catch { }
                        }
                        await _state.WriteStateAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _state.State.Errors.Add(ex.Message);
            }
            finally
            {
                _state.State.CompletedUtc = DateTime.UtcNow;
                _state.State.Running = false;
                _running = false;
                await _state.WriteStateAsync();

                var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                await notifier.NotifyContentReferenceReindexCompletedAsync(new ContentReferenceReindexCompletedNotification(
                    _state.State.Id,
                    _state.State.ReferenceType,
                    _state.State.Total,
                    _state.State.Processed,
                    _state.State.Failed,
                    Success: _state.State.Failed == 0));
            }
        });
    }
}
