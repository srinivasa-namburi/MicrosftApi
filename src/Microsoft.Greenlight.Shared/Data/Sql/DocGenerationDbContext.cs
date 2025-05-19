using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Models.DomainGroups;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Models.Validation;

namespace Microsoft.Greenlight.Shared.Data.Sql;

/// <summary>
/// The database context for the document generation system.
/// </summary>
public class DocGenerationDbContext : DbContext
{
    private readonly DbContextOptions<DocGenerationDbContext> _dbContextOptions;

    /// <summary>
    /// Creates a new instance of the <see cref="DocGenerationDbContext"/> class.
    /// </summary>
    /// <param name="dbContextOptions">The database context options.</param>
    public DocGenerationDbContext(
        DbContextOptions<DocGenerationDbContext> dbContextOptions
        )
        : base(dbContextOptions)
    {
        _dbContextOptions = dbContextOptions;
    }

    /// <summary>
    /// Configures the database context.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("ProjectVicoDB");
        }
    }

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Saves changes to the database asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<EntityBase>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedUtc = DateTime.UtcNow;
                entry.Entity.ModifiedUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedUtc = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Configures the model for the database context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //Apply configurations for entities inheriting from EntityBase
        //This loop applies configurations for all entities inheriting EntityBase to avoid repeating the same configurations
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.IsSubclassOf(typeof(EntityBase)))
            {
                modelBuilder.Entity(entityType.ClrType).HasKey(nameof(EntityBase.Id));
                modelBuilder.Entity(entityType.ClrType).Property(typeof(byte[]), nameof(EntityBase.RowVersion)).IsRowVersion();
            }
        }

        // ValueConverter for List<string> to JSON string for storage in a single column
        var stringListToJsonConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        // ValueConverter for List<Guid> to semi-colon separated string for storage in a single column
        var guidListToStringConverter = new ValueConverter<List<Guid>, string>(
            v => string.Join(";", v),
            v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList());

        var guidListComparer = new ValueComparer<List<Guid>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        // ValueConverter for Dictionary<string, string> to JSON string for storage in a single column
        var dictionaryToJsonConverter = new ValueConverter<Dictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v ?? new Dictionary<string, string>(), (JsonSerializerOptions)null!),
            v => string.IsNullOrEmpty(v) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<string, string>());

        var dictionaryComparer = new ValueComparer<Dictionary<string, string>>(
            (c1, c2) => (c1 ?? new Dictionary<string, string>()).SequenceEqual(c2 ?? new Dictionary<string, string>()),
            c => (c ?? new Dictionary<string, string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
            c => (c ?? new Dictionary<string, string>()).ToDictionary(entry => entry.Key, entry => entry.Value));

        modelBuilder.Entity<ExportedDocumentLink>()
            .ToTable("ExportedDocumentLinks");

        modelBuilder.Entity<ExportedDocumentLink>()
            .HasIndex(nameof(ExportedDocumentLink.Id))
            .IsUnique();

        modelBuilder.Entity<ExportedDocumentLink>()
            .HasIndex(nameof(ExportedDocumentLink.GeneratedDocumentId))
            .IsUnique(false);

        modelBuilder.Entity<ExportedDocumentLink>()
            .HasIndex(nameof(ExportedDocumentLink.FileHash))
            .IsUnique(false);

        modelBuilder.Entity<ContentReferenceItem>()
            .ToTable("ContentReferenceItems");

        modelBuilder.Entity<ContentReferenceItem>()
            .HasIndex(nameof(ContentReferenceItem.Id))
            .IsUnique();

        modelBuilder.Entity<ContentReferenceItem>()
            .HasIndex(nameof(ContentReferenceItem.Id), nameof(ContentReferenceItem.ReferenceType));

        modelBuilder.Entity<ContentReferenceItem>()
            .HasIndex(nameof(ContentReferenceItem.ContentReferenceSourceId))
            .IsUnique(false);
            
        modelBuilder.Entity<ContentNodeVersionTracker>()
            .ToTable("ContentNodeVersionTrackers");

        modelBuilder.Entity<ContentNodeVersionTracker>()
            .HasIndex(nameof(ContentNodeVersionTracker.ContentNodeId))
            .IsUnique();

        modelBuilder.Entity<ContentNodeVersionTracker>()
            .HasOne(x => x.ContentNode)
            .WithOne(x => x.ContentNodeVersionTracker)
            .HasForeignKey<ContentNodeVersionTracker>(x => x.ContentNodeId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentNodeVersionTracker>()
            .Property(x => x.ContentNodeVersionsJson)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<ContentEmbedding>()
            .ToTable("ContentEmbeddings");

        modelBuilder.Entity<ContentEmbedding>()
            .HasOne(x => x.ContentReferenceItem)
            .WithMany(x => x.Embeddings)
            .HasForeignKey(x => x.ContentReferenceItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiModel>(entity =>
        {
            entity.ToTable("AiModels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();

            entity.Property(e => e.TokenSettings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                    v => JsonSerializer.Deserialize<AiModelMaxTokenSettings>(v, (JsonSerializerOptions)null!) ?? new AiModelMaxTokenSettings()
                );

            entity.HasIndex(nameof(AiModel.Name)).IsUnique();
        });

        modelBuilder.Entity<AiModelDeployment>(entity =>
        {
            entity.ToTable("AiModelDeployments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeploymentName).IsRequired();
            entity.Property(e => e.AiModelId).IsRequired();

            // Always include the AiModel when querying for an AiModelDeployment
            entity.Navigation(n => n.AiModel)
                .AutoInclude();

            entity.HasOne(e => e.AiModel)
                .WithMany()
                .HasForeignKey(e => e.AiModelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.TokenSettings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                    v => JsonSerializer.Deserialize<AiModelMaxTokenSettings>(v, (JsonSerializerOptions)null!) ?? new AiModelMaxTokenSettings()
                );

            entity.Property(e => e.ReasoningSettings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                    v => JsonSerializer.Deserialize<AiModelReasoningSettings>(v, (JsonSerializerOptions)null!) ?? new AiModelReasoningSettings()
                );

            entity.HasIndex(nameof(AiModelDeployment.DeploymentName)).IsUnique();
        });

        modelBuilder.Entity<DocumentProcessValidationPipeline>()
            .ToTable("DocumentProcessValidationPipelines");

        modelBuilder.Entity<DocumentProcessValidationPipeline>()
            .HasIndex(nameof(DocumentProcessValidationPipeline.DocumentProcessId))
            .IsUnique();

        modelBuilder.Entity<DocumentProcessValidationPipeline>()
            .HasOne(x => x.DocumentProcess)
            .WithOne(x => x.ValidationPipeline)
            .HasForeignKey<DynamicDocumentProcessDefinition>(x => x.ValidationPipelineId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentProcessValidationPipeline>()
            .HasMany(x => x.ValidationPipelineSteps)
            .WithOne(x => x.DocumentProcessValidationPipeline)
            .HasForeignKey(x => x.DocumentProcessValidationPipelineId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentProcessValidationPipelineStep>()
            .ToTable("DocumentProcessValidationPipelineSteps");

        modelBuilder.Entity<DocumentProcessValidationPipelineStep>()
            .HasIndex(nameof(DocumentProcessValidationPipelineStep.DocumentProcessValidationPipelineId),
                nameof(DocumentProcessValidationPipelineStep.Order));

        modelBuilder.Entity<ValidationPipelineExecution>()
            .ToTable("ValidationPipelineExecutions");

        modelBuilder.Entity<ValidationPipelineExecution>()
            .HasIndex(nameof(ValidationPipelineExecution.DocumentProcessValidationPipelineId))
            .IsUnique(false);

        modelBuilder.Entity<ValidationPipelineExecution>()
            .HasOne(x => x.DocumentProcessValidationPipeline)
            .WithMany(x => x.ValidationPipelineExecutions)
            .HasForeignKey(x => x.DocumentProcessValidationPipelineId)
            .IsRequired();
        
        modelBuilder.Entity<ValidationPipelineExecution>()
            .HasMany(x=>x.ExecutionSteps)
            .WithOne(x => x.ValidationPipelineExecution)
            .HasForeignKey(x => x.ValidationPipelineExecutionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValidationPipelineExecution>()
            .HasOne(x=>x.GeneratedDocument)
            .WithMany(x => x.ValidationPipelineExecutions)
            .HasForeignKey(x => x.GeneratedDocumentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValidationPipelineExecutionStep>()
            .ToTable("ValidationPipelineExecutionSteps");

        modelBuilder.Entity<ValidationPipelineExecutionStep>()
            .HasIndex(nameof(ValidationPipelineExecutionStep.ValidationPipelineExecutionId),
                nameof(ValidationPipelineExecutionStep.Order));

        modelBuilder.Entity<ValidationPipelineExecutionStep>()
            .HasOne(x => x.ValidationPipelineExecution)
            .WithMany(x => x.ExecutionSteps)
            .HasForeignKey(x => x.ValidationPipelineExecutionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValidationPipelineExecutionStep>()
            .HasMany(x => x.ValidationExecutionStepContentNodeResults)
            .WithOne(x => x.ValidationPipelineExecutionStep)
            .HasForeignKey(x => x.ValidationPipelineExecutionStepId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ValidationPipelineExecutionStepResult>()
            .ToTable("ValidationPipelineExecutionStepResults");

        modelBuilder.Entity<ValidationPipelineExecutionStepResult>()
            .HasOne(x => x.ValidationPipelineExecutionStep)
            .WithOne(x => x.ValidationPipelineExecutionStepResult)
            .HasForeignKey<ValidationPipelineExecutionStepResult>(x => x.ValidationPipelineExecutionStepId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValidationPipelineExecutionStepResult>()
            .HasIndex(nameof(ValidationPipelineExecutionStepResult.ValidationPipelineExecutionStepId))
            .IsUnique();

        modelBuilder.Entity<ValidationPipelineExecutionStepResult>()
            .HasMany(x=>x.ContentNodeResults)
            .WithOne(x => x.ValidationPipelineExecutionStepResult)
            .HasForeignKey(x => x.ValidationPipelineExecutionStepResultId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValidationExecutionStepContentNodeResult>()
            .ToTable("ValidationExecutionStepContentNodeResults");

        
        modelBuilder.Entity<ValidationExecutionStepContentNodeResult>()
            .HasIndex(nameof(ValidationExecutionStepContentNodeResult.ValidationPipelineExecutionStepResultId))
            .IsUnique(false);

        modelBuilder.Entity<ValidationExecutionStepContentNodeResult>()
            .HasIndex(nameof(ValidationExecutionStepContentNodeResult.OriginalContentNodeId))
            .IsUnique(false);

        modelBuilder.Entity<ValidationExecutionStepContentNodeResult>()
            .HasIndex(nameof(ValidationExecutionStepContentNodeResult.OriginalContentNodeId),
                nameof(ValidationExecutionStepContentNodeResult.ResultantContentNodeId))
            .IsUnique(false);

        modelBuilder.Entity<ValidationExecutionStepContentNodeResult>()
            .HasIndex(nameof(ValidationExecutionStepContentNodeResult.ApplicationStatus))
            .IsUnique(false);

        modelBuilder.Entity<ValidationExecutionStepContentNodeResult>()
            .HasOne(x => x.OriginalContentNode)
            .WithMany()
            .HasForeignKey(x => x.OriginalContentNodeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.ClientSetNull);

        modelBuilder.Entity<ValidationExecutionStepContentNodeResult>()
            .HasOne(x=>x.ResultantContentNode)
            .WithMany()
            .HasForeignKey(x => x.ResultantContentNodeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.ClientSetNull);

        modelBuilder.Entity<DomainGroup>()
            .ToTable("DomainGroups");

        modelBuilder.Entity<DomainGroup>()
            .HasIndex(nameof(DomainGroup.Name))
            .IsUnique();

        modelBuilder.Entity<DomainGroup>()
            .HasMany(x => x.DocumentProcesses)
            .WithMany(x => x.DomainGroupMemberships)
            ;

        modelBuilder.Entity<ContentNodeSystemItem>()
            .ToTable("ContentNodeSystemItems");

        modelBuilder.Entity<ContentNodeSystemItem>()
            .HasIndex(nameof(ContentNodeSystemItem.ContentNodeId))
            .IsUnique();

        modelBuilder.Entity<ContentNodeSystemItem>()
            .HasMany(x => x.SourceReferences)
            .WithOne(x=>x.ContentNodeSystemItem)
            .HasForeignKey(x => x.ContentNodeSystemItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentNodeSystemItem>()
            .HasOne(x => x.ContentNode)
            .WithOne(x => x.ContentNodeSystemItem)
            .HasForeignKey<ContentNodeSystemItem>(x => x.ContentNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<SourceReferenceItem>()
             .ToTable("SourceReferenceItems");

        modelBuilder.Entity<SourceReferenceItem>()
            .HasIndex(nameof(SourceReferenceItem.ContentNodeSystemItemId))
            .IsUnique(false);

        modelBuilder.Entity<SourceReferenceItem>()
            .HasDiscriminator<string>("Discriminator")
            .HasValue<PluginSourceReferenceItem>("PluginSourceReferenceItem")
            .HasValue<KernelMemoryDocumentSourceReferenceItem>("KernelMemoryDocumentSourceReferenceItem")
            .HasValue<DocumentProcessRepositorySourceReferenceItem>("DocumentProcessRepositorySourceReferenceItem")
            .HasValue<DocumentLibrarySourceReferenceItem>("DocumentLibrarySourceReferenceItem");
            

        modelBuilder.Entity<SourceReferenceItem>()
            .Property("Discriminator")
            .HasMaxLength(100);

        modelBuilder.Entity<SourceReferenceItem>()
            .HasOne(x => x.ContentNodeSystemItem)
            .WithMany(x => x.SourceReferences)
            .HasForeignKey(x => x.ContentNodeSystemItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentLibrary>()
            .ToTable("DocumentLibraries");

        modelBuilder.Entity<DocumentLibrary>()
            .HasIndex(nameof(DocumentLibrary.ShortName))
            .IsUnique();

        modelBuilder.Entity<DocumentLibrary>()
            .HasIndex(nameof(DocumentLibrary.IndexName))
            .IsUnique();

        modelBuilder.Entity<DocumentLibrary>()
            .HasIndex(nameof(DocumentLibrary.BlobStorageContainerName))
            .IsUnique();

        modelBuilder.Entity<DocumentLibrary>()
            .HasMany(x => x.DocumentProcessAssociations)
            .WithOne(x => x.DocumentLibrary)
            .HasForeignKey(x => x.DocumentLibraryId)
            .IsRequired(true);

        modelBuilder.Entity<DocumentLibraryDocumentProcessAssociation>()
            .ToTable("DocumentLibraryDocumentProcessAssociations");

        modelBuilder.Entity<DocumentLibraryDocumentProcessAssociation>()
            .HasIndex(nameof(DocumentLibraryDocumentProcessAssociation.DocumentLibraryId), nameof(DocumentLibraryDocumentProcessAssociation.DynamicDocumentProcessDefinitionId))
            .IsUnique();

        modelBuilder.Entity<DocumentLibraryDocumentProcessAssociation>()
            .HasOne(x => x.DocumentLibrary)
            .WithMany(x => x.DocumentProcessAssociations)
            .HasForeignKey(x => x.DocumentLibraryId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentLibraryDocumentProcessAssociation>()
            .HasOne(x => x.DynamicDocumentProcessDefinition)
            .WithMany(x=>x.AdditionalDocumentLibraries)
            .HasForeignKey(x => x.DynamicDocumentProcessDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentLibraryDocumentProcessAssociation>()
            .HasIndex(nameof(DocumentLibraryDocumentProcessAssociation.DocumentLibraryId))
            .IsUnique(false);

        modelBuilder.Entity<DocumentLibraryDocumentProcessAssociation>()
            .HasIndex(nameof(DocumentLibraryDocumentProcessAssociation.DynamicDocumentProcessDefinitionId))
            .IsUnique(false);

        modelBuilder.Entity<ReviewQuestionAnswer>()
            .ToTable("ReviewQuestionAnswers");

        modelBuilder.Entity<ReviewQuestionAnswer>()
            .HasIndex(nameof(ReviewQuestionAnswer.OriginalReviewQuestionId))
            .IsUnique(false);

        modelBuilder.Entity<ReviewQuestionAnswer>()
            .HasOne(x => x.ReviewInstance)
            .WithMany(x => x.ReviewQuestionAnswers)
            .HasForeignKey(x => x.ReviewInstanceId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<ReviewQuestionAnswer>()
            .HasOne(x => x.OriginalReviewQuestion)
            .WithMany()
            .HasForeignKey(x => x.OriginalReviewQuestionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.ClientSetNull);

        modelBuilder.Entity<ReviewInstance>()
            .ToTable("ReviewInstances");

        modelBuilder.Entity<ReviewInstance>()
            .HasOne(x => x.ReviewDefinition)
            .WithMany(x => x.ReviewInstances)
            .HasForeignKey(x => x.ReviewDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewInstance>()
            .HasOne(x => x.ExportedDocumentLink)
            .WithMany()
            .HasForeignKey(x => x.ExportedLinkId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewInstance>()
            .HasIndex(nameof(ReviewInstance.ReviewDefinitionId))
            .IsUnique(false);

        modelBuilder.Entity<ReviewInstance>()
            .HasIndex(nameof(ReviewInstance.ExportedLinkId))
            .IsUnique(false);

        modelBuilder.Entity<ReviewInstance>()
            .HasIndex(nameof(ReviewInstance.Status))
            .IsUnique(false);

        modelBuilder.Entity<ReviewInstance>()
            .HasIndex(nameof(ReviewInstance.CreatedUtc))
            .IsUnique(false);

        modelBuilder.Entity<ReviewDefinition>()
            .ToTable("ReviewDefinitions");

        modelBuilder.Entity<ReviewDefinition>()
            .HasMany(x => x.ReviewQuestions)
            .WithOne(x => x.Review)
            .HasForeignKey(x => x.ReviewId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewDefinition>()
            .HasMany(x => x.ReviewInstances)
            .WithOne(x => x.ReviewDefinition)
            .HasForeignKey(x => x.ReviewDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewDefinition>()
            .HasMany(x => x.DocumentProcessDefinitionConnections)
            .WithOne(x => x.Review)
            .HasForeignKey(x => x.ReviewId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewQuestion>()
            .ToTable("ReviewQuestions");

        modelBuilder.Entity<ReviewQuestion>()
            .HasIndex(nameof(ReviewQuestion.ReviewId))
            .IsUnique(false);

        modelBuilder.Entity<ReviewQuestion>()
            .HasOne(x => x.Review)
            .WithMany(x => x.ReviewQuestions)
            .HasForeignKey(x => x.ReviewId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewDefinitionDocumentProcessDefinition>()
            .ToTable("ReviewDefinitionDocumentProcessDefinition");

        modelBuilder.Entity<ReviewDefinitionDocumentProcessDefinition>()
            .HasIndex(nameof(ReviewDefinitionDocumentProcessDefinition.ReviewId), nameof(ReviewDefinitionDocumentProcessDefinition.DocumentProcessDefinitionId))
            .IsUnique();

        modelBuilder.Entity<ReviewDefinitionDocumentProcessDefinition>()
            .HasOne(x => x.Review)
            .WithMany(x => x.DocumentProcessDefinitionConnections)
            .HasForeignKey(x => x.ReviewId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewDefinitionDocumentProcessDefinition>()
            .HasOne(x => x.DocumentProcessDefinition)
            .WithMany()
            .HasForeignKey(x => x.DocumentProcessDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewDefinitionDocumentProcessDefinition>()
            .HasIndex(nameof(ReviewDefinitionDocumentProcessDefinition.IsActive))
            .IsUnique(false);

        modelBuilder.Entity<ReviewDefinitionDocumentProcessDefinition>()
            .HasIndex(nameof(ReviewDefinitionDocumentProcessDefinition.ReviewId))
            .IsUnique(false);

        modelBuilder.Entity<ReviewDefinitionDocumentProcessDefinition>()
            .HasIndex(nameof(ReviewDefinitionDocumentProcessDefinition.DocumentProcessDefinitionId))
            .IsUnique(false);

        modelBuilder.Entity<DocumentOutline>()
            .ToTable("DocumentOutlines");

        modelBuilder.Entity<DocumentOutline>()
            .HasOne(x => x.DocumentProcessDefinition)
            .WithOne(x => x.DocumentOutline)
            .HasForeignKey<DynamicDocumentProcessDefinition>(x => x.DocumentOutlineId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        //For the DocumentOutline entity, don't store the Text property in the database
        modelBuilder.Entity<DocumentOutline>()
            .Ignore(x => x.FullText);

        modelBuilder.Entity<DocumentOutline>()
            .HasIndex(nameof(DocumentOutline.DocumentProcessDefinitionId))
            .IsUnique();

        modelBuilder.Entity<DocumentOutline>()
            .HasMany(x => x.OutlineItems)
            .WithOne(x => x.DocumentOutline)
            .HasForeignKey(x => x.DocumentOutlineId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        // Auto Include OutlineItems in the context
        modelBuilder.Entity<DocumentOutline>()
            .Navigation(x => x.OutlineItems)
            .AutoInclude();


        modelBuilder.Entity<DocumentOutlineItem>()
            .ToTable("DocumentOutlineItems");

        modelBuilder.Entity<DocumentOutlineItem>()
            .HasOne(x => x.DocumentOutline)
            .WithMany(x => x.OutlineItems)
            .HasForeignKey(x => x.DocumentOutlineId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentOutlineItem>()
            .HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DocumentOutlineItem>()
            .HasIndex(nameof(DocumentOutlineItem.DocumentOutlineId))
            .IsUnique(false);
        modelBuilder.Entity<DocumentOutlineItem>()
            .HasIndex(nameof(DocumentOutlineItem.ParentId))
            .IsUnique(false);
        modelBuilder.Entity<DocumentOutlineItem>()
            .HasIndex(nameof(DocumentOutlineItem.SectionNumber))
            .IsUnique(false);

        // Add this line to configure the OrderIndex property
        modelBuilder.Entity<DocumentOutlineItem>()
            .Property(x => x.OrderIndex)
            .IsRequired(false);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .ToTable("DynamicDocumentProcessDefinitions");

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasOne(x => x.AiModelDeployment)
            .WithMany()
            .HasForeignKey(x => x.AiModelDeploymentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasOne(x => x.AiModelDeploymentForValidation)
            .WithMany()
            .HasForeignKey(x => x.AiModelDeploymentForValidationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasOne(x => x.DocumentOutline)
            .WithOne(x => x.DocumentProcessDefinition)
            .HasForeignKey<DocumentOutline>(x => x.DocumentProcessDefinitionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasIndex(nameof(DynamicDocumentProcessDefinition.ShortName))
            .IsUnique();

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasIndex(nameof(DynamicDocumentProcessDefinition.LogicType))
            .IsUnique(false);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .Property(e => e.Repositories)
            .HasConversion(stringListToJsonConverter)
            .Metadata.SetValueComparer(stringListComparer);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasMany(x => x.MetaDataFields)
            .WithOne(x => x.DynamicDocumentProcessDefinition)
            .HasForeignKey(x => x.DynamicDocumentProcessDefinitionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasMany(x => x.AdditionalDocumentLibraries)
            .WithOne(x => x.DynamicDocumentProcessDefinition)
            .HasForeignKey(x => x.DynamicDocumentProcessDefinitionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasOne(x => x.ValidationPipeline)
            .WithOne(x => x.DocumentProcess)
            .HasForeignKey<DynamicDocumentProcessDefinition>(x => x.ValidationPipelineId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasMany(x => x.McpServerAssociations)
            .WithOne(x => x.DynamicDocumentProcessDefinition)
            .HasForeignKey(x => x.DynamicDocumentProcessDefinitionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PromptDefinition>()
            .ToTable("PromptDefinitions");

        modelBuilder.Entity<PromptDefinition>()
            .HasIndex(nameof(PromptDefinition.ShortCode))
            .IsUnique();

        modelBuilder.Entity<PromptDefinition>()
            .HasMany(x => x.Implementations)
            .WithOne(x => x.PromptDefinition)
            .HasForeignKey(x => x.PromptDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<PromptDefinition>()
            .HasMany(x => x.Variables)
            .WithOne(x => x.PromptDefinition)
            .HasForeignKey(x => x.PromptDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PromptImplementation>()
            .ToTable("PromptImplementations");

        modelBuilder.Entity<PromptImplementation>()
            .HasIndex(nameof(PromptImplementation.PromptDefinitionId), nameof(PromptImplementation.DocumentProcessDefinitionId))
            .IsUnique();

        modelBuilder.Entity<PromptImplementation>()
            .HasOne(x => x.DocumentProcessDefinition)
            .WithMany(x => x.Prompts)
            .HasForeignKey(x => x.DocumentProcessDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PromptVariableDefinition>()
            .ToTable("PromptVariableDefinitions");

        modelBuilder.Entity<PromptVariableDefinition>()
            .HasIndex(nameof(PromptVariableDefinition.PromptDefinitionId), nameof(PromptVariableDefinition.VariableName))
            .IsUnique();

        modelBuilder.Entity<PromptVariableDefinition>()
            .HasIndex(nameof(PromptVariableDefinition.PromptDefinitionId))
            .IsUnique(false);

        modelBuilder.Entity<PromptVariableDefinition>()
            .HasOne(x => x.PromptDefinition)
            .WithMany(x => x.Variables)
            .HasForeignKey(x => x.PromptDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

       
        modelBuilder.Entity<ChatConversation>()
            .ToTable("ChatConversations");

        modelBuilder.Entity<ChatConversation>()
            .HasMany(x => x.ChatMessages)
            .WithOne(x => x.Conversation)
            .HasForeignKey(x => x.ConversationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatConversation>()
            .Property(e => e.ReferenceItemIds)
            .HasConversion(guidListToStringConverter)
            .Metadata.SetValueComparer(guidListComparer);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.Conversation)
            .WithMany(x => x.ChatMessages)
            .HasForeignKey(x => x.ConversationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserInformation>()
            .HasIndex(nameof(UserInformation.ProviderSubjectId))
            .IsUnique();
        modelBuilder.Entity<UserInformation>()
            .HasIndex(nameof(UserInformation.Email))
            .IsUnique(false);

        modelBuilder.Entity<ConversationSummary>()
            .HasIndex(nameof(ConversationSummary.CreatedAt))
            .IsUnique(false);

        modelBuilder.Entity<ConversationSummary>()
            .HasIndex(nameof(ConversationSummary.ConversationId))
            .IsUnique(false);

        modelBuilder.Entity<ConversationSummary>()
            .HasMany(x => x.SummarizedChatMessages)
            .WithOne(x => x.SummarizedByConversationSummary)
            .HasForeignKey(x => x.SummarizedByConversationSummaryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.SummarizedByConversationSummary)
            .WithMany(x => x.SummarizedChatMessages)
            .HasForeignKey(x => x.SummarizedByConversationSummaryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(nameof(ChatMessage.CreatedUtc))
            .IsUnique(false);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(nameof(ChatMessage.ConversationId))
            .IsUnique(false);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(nameof(ChatMessage.ReplyToChatMessageId))
            .IsUnique(false);

        // Self-Referencing relationship for ChatMessages - a message may be a reply to another message
        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.ReplyToChatMessage)
            .WithOne()
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IngestedDocument>()
            .HasIndex(d => d.FileHash)
            .IsUnique(false);

        modelBuilder.Entity<IngestedDocument>()
            .HasIndex(d => d.DocumentProcess)
            .IsUnique(false);

        modelBuilder.Entity<ContentNode>()
            .HasOne(x => x.ContentNodeVersionTracker)
            .WithOne(x => x.ContentNode)
            .HasForeignKey<ContentNode>(x => x.ContentNodeVersionTrackerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ContentNode>()
            .HasIndex(nameof(ContentNode.ContentNodeVersionTrackerId))
            .IsUnique(false);

        // Generated Documents and their relationships
        modelBuilder.Entity<GeneratedDocument>()
            .HasMany(d => d.ContentNodes)
            .WithOne(x => x.GeneratedDocument)
            .HasForeignKey(x => x.GeneratedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GeneratedDocument>()
            .HasMany(d => d.ExportedDocumentLinks)
            .WithOne(x => x.GeneratedDocument)
            .HasForeignKey(x => x.GeneratedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentNode>()
            .HasOne(x => x.GeneratedDocument)
            .WithMany(x => x.ContentNodes)
            .HasForeignKey(x => x.GeneratedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContentNode>()
            .HasOne(x=>x.AssociatedGeneratedDocument)
            .WithMany()
            .HasForeignKey(x => x.AssociatedGeneratedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GeneratedDocument>()
            .HasOne(x => x.Metadata)
            .WithOne(x => x.GeneratedDocument)
            .IsRequired(false)
            .HasForeignKey<DocumentMetadata>(x => x.GeneratedDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure the self-referencing relationship of ContentNode
        modelBuilder.Entity<ContentNode>()
            .HasMany(c => c.Children)
            .WithOne(c => c.Parent)
            .HasForeignKey(c => c.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<McpPlugin>(entity =>
        {
            entity.ToTable("McpPlugins");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();

            entity.Property(e => e.SourceType)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(e => e.BlobContainerName)
                .IsRequired(false);

            entity.HasMany(e => e.Versions)
                .WithOne(e => e.McpPlugin)
                .HasForeignKey(e => e.McpPluginId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<McpPluginVersion>(entity =>
        {
            entity.ToTable("McpPluginVersions");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.McpPlugin)
                .WithMany(e => e.Versions)
                .HasForeignKey(e => e.McpPluginId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Major).IsRequired();
            entity.Property(e => e.Minor).IsRequired();
            entity.Property(e => e.Patch).IsRequired();

            // Configure Arguments to store as semicolon-separated string
            entity.Property(e => e.Arguments)
                .HasConversion(
                    v => string.Join(";", v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    new ValueComparer<List<string>>(
                        (c1, c2) => c1!.SequenceEqual(c2!),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    )
                );

            // Configure EnvironmentVariables to store as JSON string
            entity.Property(e => e.EnvironmentVariables)
                .HasConversion(dictionaryToJsonConverter)
                .Metadata.SetValueComparer(dictionaryComparer);

            entity.HasIndex(e => new { e.McpPluginId, e.Major, e.Minor, e.Patch })
                .IsUnique();
        });

        modelBuilder.Entity<McpPluginDocumentProcess>(entity =>
        {
            entity.ToTable("McpPluginDocumentProcesses");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.McpPlugin)
                .WithMany(e => e.DocumentProcesses)
                .HasForeignKey(e => e.McpPluginId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DynamicDocumentProcessDefinition)
                .WithMany(e => e.McpServerAssociations)
                .HasForeignKey(e => e.DynamicDocumentProcessDefinitionId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Version)
                .WithMany()
                .HasForeignKey(e => e.VersionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.McpPluginId, e.DynamicDocumentProcessDefinitionId })
                .IsUnique(false);
        });
    }
    
    /// <summary>
    /// Gets or sets the source reference items.
    /// </summary>
    public DbSet<SourceReferenceItem> SourceReferenceItems { get; set; }

    /// <summary>
    /// Gets or sets the content node system items.
    /// </summary>
    public virtual DbSet<ContentNodeSystemItem> ContentNodeSystemItems { get; set; }

    /// <summary>
    /// Gets or sets the generated documents.
    /// </summary>
    public virtual DbSet<GeneratedDocument> GeneratedDocuments { get; set; }

    /// <summary>
    /// Gets or sets the exported document links.
    /// </summary>
    public virtual DbSet<ExportedDocumentLink> ExportedDocumentLinks { get; set; }

    /// <summary>
    /// Gets or sets the document metadata.
    /// </summary>
    public virtual DbSet<DocumentMetadata> DocumentMetadata { get; set; }

    /// <summary>
    /// Gets or sets the content nodes.
    /// </summary>
    public virtual DbSet<ContentNode> ContentNodes { get; set; }

    /// <summary>
    /// Gets or sets the content node version trackers.
    /// </summary>
    public virtual DbSet<ContentNodeVersionTracker> ContentNodeVersionTrackers { get; set; }

    /// <summary>
    /// Gets or sets the ingested documents.
    /// </summary>
    public DbSet<IngestedDocument> IngestedDocuments { get; set; }

    /// <summary>
    /// Gets or sets the chat conversations.
    /// </summary>
    public DbSet<ChatConversation> ChatConversations { get; set; }

    /// <summary>
    /// Gets or sets the chat messages.
    /// </summary>
    public DbSet<ChatMessage> ChatMessages { get; set; }

    /// <summary>
    /// Gets or sets the converstation summaries.
    /// </summary>
    public DbSet<ConversationSummary> ConversationSummaries { get; set; }

    /// <summary>
    /// Gets or sets the user information.
    /// </summary>
    public DbSet<UserInformation> UserInformations { get; set; }

    /// <summary>
    /// Gets or sets the dynamic document process definitions.
    /// </summary>
    public DbSet<DynamicDocumentProcessDefinition> DynamicDocumentProcessDefinitions { get; set; }

    /// <summary>
    /// Gets or sets the dynamic document process meta data fields.
    /// </summary>
    public DbSet<DynamicDocumentProcessMetaDataField> DynamicDocumentProcessMetaDataFields { get; set; }

    /// <summary>
    /// Gets or sets the document outlines.
    /// </summary>
    public DbSet<DocumentOutline> DocumentOutlines { get; set; }

    /// <summary>
    /// Gets or sets the document outline items.
    /// </summary>
    public DbSet<DocumentOutlineItem> DocumentOutlineItems { get; set; }

    /// <summary>
    /// Gets or sets the prompt definitions.
    /// </summary>
    public DbSet<PromptDefinition> PromptDefinitions { get; set; }

    /// <summary>
    /// Gets or sets the prompt variable definitions.
    /// </summary>
    public DbSet<PromptVariableDefinition> PromptVariableDefinitions { get; set; }

    /// <summary>
    /// Gets or sets the prompt implementations.
    /// </summary>
    public DbSet<PromptImplementation> PromptImplementations { get; set; }

    /// <summary>
    /// Gets or sets the review definitions.
    /// </summary>
    public virtual DbSet<ReviewDefinition> ReviewDefinitions { get; set; }

    /// <summary>
    /// Gets or sets the review questions.
    /// </summary>
    public virtual DbSet<ReviewQuestion> ReviewQuestions { get; set; }

    /// <summary>
    /// Gets or sets the review definition document process definitions.
    /// </summary>
    public DbSet<ReviewDefinitionDocumentProcessDefinition> ReviewDefinitionDocumentProcessDefinitions { get; set; }

    /// <summary>
    /// Gets or sets the review instances.
    /// </summary>
    public virtual DbSet<ReviewInstance> ReviewInstances { get; set; }

    /// <summary>
    /// Gets or sets the review question answers.
    /// </summary>
    public DbSet<ReviewQuestionAnswer> ReviewQuestionAnswers { get; set; }

    /// <summary>
    /// Gets or sets the document libraries.
    /// </summary>
    public DbSet<DocumentLibrary> DocumentLibraries { get; set; }

    /// <summary>
    /// Gets or sets the document library document process associations.
    /// </summary>
    public DbSet<DocumentLibraryDocumentProcessAssociation> DocumentLibraryDocumentProcessAssociations { get; set; }

    /// <summary>
    /// Gets or sets the domain groups.
    /// </summary>
    public DbSet<DomainGroup> DomainGroups { get; set; }

    /// <summary>
    /// Gets or sets the database configurations.
    /// </summary>
    public DbSet<DbConfiguration> Configurations { get; set; } = null!;

    /// <summary>
    /// Gets or sets the document process validation pipelines.
    /// </summary>
    public DbSet<DocumentProcessValidationPipeline> DocumentProcessValidationPipelines { get; set; }

    /// <summary>
    /// Gets or sets the document process validation pipeline steps.
    /// </summary>
    public DbSet<DocumentProcessValidationPipelineStep> DocumentProcessValidationPipelineSteps { get; set; }
    /// <summary>
    /// Gets or sets the validation pipeline executions
    /// </summary>
    public DbSet<ValidationPipelineExecution> ValidationPipelineExecutions { get; set; }

    /// <summary>
    /// Gets or sets the validation pipeline execution
    /// </summary>
    public DbSet<ValidationPipelineExecutionStep> ValidationPipelineExecutionSteps { get; set; }
    /// <summary>
    /// Gets or sets the validation pipeline execution step results
    /// </summary>
    public DbSet<ValidationPipelineExecutionStepResult> ValidationPipelineExecutionStepResults { get; set; }

    /// <summary>
    /// Gets or sets the validation execution step content node results
    /// </summary>
    public DbSet<ValidationExecutionStepContentNodeResult> ValidationExecutionStepContentNodeResults { get; set; }

    /// <summary>
    /// Gets or sets the AI Models
    /// </summary>
    public DbSet<AiModel?> AiModels { get; set; }

    /// <summary>
    /// Gets or sets the AI Model Deployments
    /// </summary>
    public DbSet<AiModelDeployment> AiModelDeployments { get; set; }

    /// <summary>
    /// Reference items included for various purposes - normally in chat messages
    /// </summary>
    public DbSet<ContentReferenceItem> ContentReferenceItems { get; set; }

    /// <summary>
    /// Embeddings generated from content reference item
    /// </summary>
    public DbSet<ContentEmbedding> ContentEmbeddings { get; set; }

    /// <summary>
    /// Gets or sets the MCP plugins.
    /// </summary>
    public DbSet<McpPlugin> McpPlugins { get; set; }

    /// <summary>
    /// Gets or sets the MCP plugin versions.
    /// </summary>
    public DbSet<McpPluginVersion> McpPluginVersions { get; set; }

    /// <summary>
    /// Gets or sets the associations between MCP plugins and document processes.
    /// </summary>
    public DbSet<McpPluginDocumentProcess> McpPluginDocumentProcesses { get; set; }
}

