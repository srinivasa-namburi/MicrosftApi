using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Grains.Document.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Enums;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Document;

/// <summary>
/// Orchestrates the document generation process, replacing the MassTransit-based DocumentGenerationSaga
/// </summary>
[Reentrant]
public class DocumentGenerationOrchestrationGrain : Grain, IDocumentGenerationOrchestrationGrain
{
    private readonly IPersistentState<DocumentGenerationState> _state;
    private readonly ILogger<DocumentGenerationOrchestrationGrain> _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);


    public DocumentGenerationOrchestrationGrain(
        [PersistentState("documentGeneration")]
        IPersistentState<DocumentGenerationState> state,
        ILogger<DocumentGenerationOrchestrationGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.Id == Guid.Empty)
        {
            _state.State.Id = this.GetPrimaryKey();
            await SafeWriteStateAsync();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<DocumentGenerationState> GetStateAsync()
    {
        return _state.State;
    }

    public async Task StartDocumentGenerationAsync(GenerateDocumentDTO request)
    {
        try
        {
            _logger.LogInformation("Starting document generation process for document {Id}", this.GetPrimaryKey());

            // Store initial state
            _state.State.DocumentTitle = request.DocumentTitle;
            _state.State.AuthorOid = request.AuthorOid;
            _state.State.DocumentProcessName = request.DocumentProcessName;
            _state.State.MetadataJson = request.RequestAsJson;
            _state.State.Status = DocumentGenerationStatus.Creating;
            _state.State.CorrelationId = this.GetPrimaryKey();
            await SafeWriteStateAsync();

            // Get document creation grain and start the process
            var documentCreatorGrain = GrainFactory.GetGrain<IDocumentCreatorGrain>(Guid.NewGuid());
            _ = documentCreatorGrain.CreateDocumentAsync(_state.State.Id, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting document generation for document {Id}", this.GetPrimaryKey());
            await HandleFailureAsync("Failed to start document generation", ex.Message);
        }
    }

    public async Task OnDocumentCreatedAsync(Guid metadataId)
    {
        try
        {
            _logger.LogInformation("Document created with metadata ID {MetadataId} for document {Id}",
                metadataId, this.GetPrimaryKey());

            _state.State.MetadataId = metadataId;
            _state.State.Status = DocumentGenerationStatus.Processing;
            await SafeWriteStateAsync();

            // Then start the document outline generation
            var outlineGeneratorGrain = GrainFactory.GetGrain<IDocumentOutlineGeneratorGrain>(this.GetPrimaryKey());
            _ = outlineGeneratorGrain.GenerateOutlineAsync(
                _state.State.Id,
                _state.State.DocumentTitle,
                _state.State.AuthorOid,
                _state.State.DocumentProcessName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling document created notification for document {Id}", this.GetPrimaryKey());
            await HandleFailureAsync("Failed during document creation", ex.Message);
        }
    }

    public async Task OnDocumentOutlineGeneratedAsync(string generatedDocumentJson)
    {
        try
        {
            _logger.LogInformation("Document outline generated for document {Id}", this.GetPrimaryKey());

            _state.State.Status = DocumentGenerationStatus.ContentGeneration;
            await SafeWriteStateAsync();

            // Publish SignalR notification
            await PublishNotificationAsync(new DocumentOutlineGeneratedNotification(_state.State.Id)
            {
                AuthorOid = _state.State.AuthorOid
            });

            // Then start content generation
            var reportContentGeneratorGrain = GrainFactory.GetGrain<IReportContentGeneratorGrain>(_state.State.Id);
            _ = reportContentGeneratorGrain.GenerateContentAsync(
                _state.State.Id,
                _state.State.AuthorOid,
                generatedDocumentJson,
                _state.State.DocumentProcessName,
                _state.State.MetadataId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling document outline generation for document {Id}", this.GetPrimaryKey());
            await HandleFailureAsync("Failed during outline generation", ex.Message);
        }
    }

    public async Task OnReportContentGenerationSubmittedAsync(int numberOfContentNodesToGenerate)
    {
        try
        {
            _logger.LogInformation("Report content generation submitted with {Count} nodes for document {Id}",
                numberOfContentNodesToGenerate, this.GetPrimaryKey());

            _state.State.NumberOfContentNodesToGenerate = numberOfContentNodesToGenerate;
            _state.State.NumberOfContentNodesGenerated = 0;
            await SafeWriteStateAsync();

            // Content nodes will be generated asynchronously by ReportTitleSectionGenerator grains
            // and will call back via OnContentNodeGeneratedAsync
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling content generation submission for document {Id}", this.GetPrimaryKey());
            await HandleFailureAsync("Failed during content generation submission", ex.Message);
        }
    }

    public async Task OnContentNodeStateChangedAsync(Guid contentNodeId, ContentNodeGenerationState state)
    {
        try
        {
            _logger.LogInformation("Content node {NodeId} state changed to {State} for document {Id}",
                contentNodeId, state, this.GetPrimaryKey());
            await PublishNotificationAsync(new ContentNodeGenerationStateChanged(_state.State.Id)
            {
                ContentNodeId = contentNodeId,
                GenerationState = state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling content node state change for document {Id}", this.GetPrimaryKey());
        }
    }

    public async Task OnContentNodeGeneratedAsync(Guid contentNodeId, bool isSuccessful)
    {
        try
        {
            if (isSuccessful)
            {
                _state.State.NumberOfContentNodesGenerated++;
                await SafeWriteStateAsync();

                _logger.LogInformation("Content node {NodeId} generated ({Current}/{Total}) for document {Id}",
                    contentNodeId,
                    _state.State.NumberOfContentNodesGenerated,
                    _state.State.NumberOfContentNodesToGenerate,
                    this.GetPrimaryKey());

                await PublishNotificationAsync(new ContentNodeGenerationStateChanged(_state.State.Id)
                {
                    ContentNodeId = contentNodeId, 
                    GenerationState = ContentNodeGenerationState.Completed
                });

                // Check if all content nodes have been generated
                if (_state.State.NumberOfContentNodesGenerated >= _state.State.NumberOfContentNodesToGenerate)
                {
                    await FinalizeContentGenerationAsync();
                }
            }
            else
            {
                _logger.LogWarning("Content node {NodeId} generation failed for document {Id}",
                    contentNodeId, this.GetPrimaryKey());

                await PublishNotificationAsync(new ContentNodeGenerationStateChanged(_state.State.Id)
                {
                    ContentNodeId = contentNodeId,
                    GenerationState = ContentNodeGenerationState.Failed
                });

                // We still increment the counter to ensure we eventually complete the process
                _state.State.NumberOfContentNodesGenerated++;
                await SafeWriteStateAsync();

                // Check if all content nodes have been generated (even with failures)
                if (_state.State.NumberOfContentNodesGenerated >= _state.State.NumberOfContentNodesToGenerate)
                {
                    await FinalizeContentGenerationAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling content node generation for document {Id}", this.GetPrimaryKey());
            // Don't fail the whole process for a single node failure
        }
    }

    public async Task OnDocumentOutlineGenerationFailedAsync()
    {
        await HandleFailureAsync("Document outline generation failed", "Failed to generate document outline");
    }

    private async Task FinalizeContentGenerationAsync()
    {
        _logger.LogInformation("Finalizing content generation for document {Id}", this.GetPrimaryKey());

        _state.State.Status = DocumentGenerationStatus.ContentFinalized;
        await SafeWriteStateAsync();

        // The Grain should deactivate/dispose at this time
        // Any final notifications or cleanup could happen here
    }

    private async Task HandleFailureAsync(string reason, string details)
    {
        _logger.LogError("Document generation failed for document {Id}: {Reason} - {Details}",
            this.GetPrimaryKey(), reason, details);

        _state.State.Status = DocumentGenerationStatus.Failed;
        _state.State.FailureReason = reason;
        _state.State.FailureDetails = details;
        await SafeWriteStateAsync();

        // Publish any failure notifications
        await PublishNotificationAsync(new DocumentOutlineGenerationFailed(_state.State.Id));
    }

    private async Task PublishNotificationAsync<T>(T notification)
    {
        try
        {
            // Get the SignalR notifier grain
            var notifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
        
            if (notification is DocumentOutlineGeneratedNotification outlineNotification)
            {
                await notifierGrain.NotifyDocumentOutlineGeneratedAsync(outlineNotification);
            }
            else if (notification is ContentNodeGenerationStateChanged stateChangedNotification)
            {
                await notifierGrain.NotifyContentNodeStateChangedAsync(stateChangedNotification);
            }
            else if (notification is DocumentOutlineGenerationFailed failedNotification)
            {
                await notifierGrain.NotifyDocumentOutlineGenerationFailedAsync(failedNotification);
            }
            else
            {
                _logger.LogWarning("Unsupported notification type: {NotificationType}", typeof(T).Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing notification of type {NotificationType}", typeof(T).Name);
        }
    }

    private async Task SafeWriteStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            _state.State.LastUpdatedUtc = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
        finally
        {
            _stateLock.Release();
        }
    }
}
