using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Models.DomainGroups;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.SagaState;

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
            .HasValue<DocumentLibrarySourceReferenceItem>("DocumentLibrarySourceReferenceItem")
            .HasValue<PluginSourceReferenceItem>("PluginSourceReferenceItem");

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

        modelBuilder.Entity<DynamicPlugin>()
            .ToTable("DynamicPlugins");

        modelBuilder.Entity<DynamicPlugin>()
            .Property(dp => dp.Versions)
            .HasConversion(
                v => string.Join(";", v.Select(x => x.ToString())),
                v => v.Split(";", StringSplitOptions.RemoveEmptyEntries).Select(DynamicPluginVersion.Parse).ToList(),
                new ValueComparer<List<DynamicPluginVersion>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                )
            );

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

        modelBuilder.Entity<DynamicPluginDocumentProcess>()
            .ToTable("DynamicPluginDocumentProcesses")
            .HasKey(dp => new { dp.DynamicPluginId, dp.DynamicDocumentProcessDefinitionId });

        modelBuilder.Entity<DynamicPluginDocumentProcess>()
            .HasOne(dp => dp.DynamicPlugin)
            .WithMany(p => p.DocumentProcesses)
            .HasForeignKey(dp => dp.DynamicPluginId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DynamicPluginDocumentProcess>()
            .HasOne(dp => dp.DynamicDocumentProcessDefinition)
            .WithMany(d => d.Plugins)
            .HasForeignKey(dp => dp.DynamicDocumentProcessDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DynamicPluginDocumentProcess>()
            .Property(dp => dp.Version)
            .HasConversion(
                v => v!.ToString(),
                v => DynamicPluginVersion.Parse(v),
                new ValueComparer<DynamicPluginVersion>(
                    (c1, c2) => c1!.CompareTo(c2!) == 0,
                    c => c.GetHashCode(),
                    c => c
                )
            );

        modelBuilder.Entity<DynamicDocumentProcessMetaDataField>()
            .ToTable("DynamicDocumentProcessMetaDataFields");

        modelBuilder.Entity<DynamicDocumentProcessMetaDataField>()
            .HasIndex(nameof(DynamicDocumentProcessMetaDataField.DynamicDocumentProcessDefinitionId))
            .IsUnique(false);

        modelBuilder.Entity<DynamicDocumentProcessMetaDataField>()
            .HasIndex(nameof(DynamicDocumentProcessMetaDataField.DynamicDocumentProcessDefinitionId), nameof(DynamicDocumentProcessMetaDataField.Name))
            .IsUnique();

        modelBuilder.Entity<DynamicDocumentProcessMetaDataField>()
            .HasOne(x => x.DynamicDocumentProcessDefinition)
            .WithMany(x => x.MetaDataFields)
            .HasForeignKey(x => x.DynamicDocumentProcessDefinitionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DynamicDocumentProcessMetaDataField>()
            .Property(e => e.PossibleValues)
            .HasConversion(stringListToJsonConverter)
            .Metadata.SetValueComparer(stringListComparer);
        
        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .ToTable("DynamicDocumentProcessDefinitions");

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
            .HasMany(x=>x.MetaDataFields)
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

        modelBuilder.Entity<IngestedDocument>()
            .HasMany(d => d.Tables)
            .WithOne(x => x.IngestedDocument)
            .HasForeignKey(x => x.IngestedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Table>()
            .HasOne(x => x.IngestedDocument)
            .WithMany(x => x.Tables)
            .HasForeignKey(x => x.IngestedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IngestedDocument>()
            .HasMany(d => d.ContentNodes)
            .WithOne(x => x.IngestedDocument)
            .HasForeignKey(x => x.IngestedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContentNode>()
            .HasOne(x => x.IngestedDocument)
            .WithMany(x => x.ContentNodes)
            .HasForeignKey(x => x.IngestedDocumentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

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

        // Optionally, ContentNodes can have Bounding Regions if they are created from an Ingested Document
        modelBuilder.Entity<ContentNode>()
            .HasMany(c => c.BoundingRegions)
            .WithOne(x => x.ContentNode)
            .HasForeignKey(x => x.ContentNodeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BoundingRegion>()
            .HasOne(x => x.ContentNode)
            .WithMany(x => x.BoundingRegions)
            .HasForeignKey(x => x.ContentNodeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Table structures for ingested documents
        modelBuilder.Entity<Table>()
            .HasMany(t => t.Cells)
            .WithOne(c => c.Table)
            .HasForeignKey(c => c.TableId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TableCell>()
            .HasOne(c => c.Table)
            .WithMany(t => t.Cells)
            .HasForeignKey(c => c.TableId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Table>()
            .HasMany(t => t.BoundingRegions)
            .WithOne(x => x.Table)
            .HasForeignKey(x => x.TableId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BoundingRegion>()
            .HasOne(x => x.Table)
            .WithMany(x => x.BoundingRegions)
            .HasForeignKey(x => x.TableId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<BoundingRegion>()
            .HasIndex(nameof(BoundingRegion.Page))
            .IsUnique(false);

        modelBuilder.Entity<BoundingPolygon>()
            .HasIndex(nameof(BoundingPolygon.X), nameof(BoundingPolygon.Y));

        modelBuilder.Entity<BoundingRegion>()
            .HasOne(x => x.Table)
            .WithMany(x => x.BoundingRegions)
            .HasForeignKey(x => x.TableId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BoundingRegion>()
            .HasMany(br => br.BoundingPolygons)
            .WithOne(bp => bp.BoundingRegion)
            .HasForeignKey(bp => bp.BoundingRegionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BoundingPolygon>()
            .HasOne(bp => bp.BoundingRegion)
            .WithMany(br => br.BoundingPolygons)
            .HasForeignKey(bp => bp.BoundingRegionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BoundingPolygon>()
            .Property<decimal>(x => x.X).HasPrecision(12, 6);
        modelBuilder.Entity<BoundingPolygon>()
            .Property<decimal>(x => x.Y).HasPrecision(12, 6);



        // Mass Transit SAGA for Document Generation
        modelBuilder.Entity<DocumentGenerationSagaState>()
            .ToTable("DocumentGenerationSagaStates");
        modelBuilder.Entity<DocumentGenerationSagaState>()
            .HasKey(x => x.CorrelationId);

        // Table per hierarchy for Document Ingestion SAGA
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasDiscriminator<string>("Discriminator")
            .HasValue<DocumentIngestionSagaState>("DocumentIngestionSagaState")
            .HasValue<KernelMemoryDocumentIngestionSagaState>("KernelMemoryDocumentIngestionSagaState");

        modelBuilder.Entity<DocumentIngestionSagaState>()
            .Property("Discriminator")
            .HasMaxLength(100);

        // Mass Transit SAGA for Review Execution
        modelBuilder.Entity<ReviewExecutionSagaState>()
            .ToTable("ReviewExecutionSagaStates");
        modelBuilder.Entity<ReviewExecutionSagaState>()
            .HasKey(x => x.CorrelationId);
        modelBuilder.Entity<ReviewExecutionSagaState>()
            .HasIndex(x => x.ExportedDocumentLinkId)
            .IsUnique(false);
        
        // Mass Transit SAGA for Document Ingestion
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .ToTable("DocumentIngestionSagaStates");
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasKey(x => x.CorrelationId);
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasIndex(x => x.FileHash)
            .IsUnique(false);
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasIndex(x => x.DocumentLibraryShortName)
            .IsUnique(false);

        // Mass Transit SAGA for Kernel Memory Document Ingestion
        // The Key is the same as the base class - this is an EF Core requirement for Table per hierarchy
        modelBuilder.Entity<KernelMemoryDocumentIngestionSagaState>()
            .HasIndex(x => x.FileHash)
            .IsUnique(false);
        modelBuilder.Entity<KernelMemoryDocumentIngestionSagaState>()
            .HasIndex(x => x.DocumentLibraryShortName)
            .IsUnique(false);

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
    /// Gets or sets the ingested documents.
    /// </summary>
    public DbSet<IngestedDocument> IngestedDocuments { get; set; }

    /// <summary>
    /// Gets or sets the tables.
    /// </summary>
    public DbSet<Table> Tables { get; set; }

    /// <summary>
    /// Gets or sets the table cells.
    /// </summary>
    public DbSet<TableCell> TableCells { get; set; }

    /// <summary>
    /// Gets or sets the bounding regions.
    /// </summary>
    public DbSet<BoundingRegion> BoundingRegions { get; set; }

    /// <summary>
    /// Gets or sets the bounding polygons.
    /// </summary>
    public DbSet<BoundingPolygon> BoundingPolygons { get; set; }

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
    /// Gets or sets the dynamic plugins.
    /// </summary>
    public DbSet<DynamicPlugin> DynamicPlugins { get; set; }

    /// <summary>
    /// Gets or sets the dynamic plugin document processes.
    /// </summary>
    public DbSet<DynamicPluginDocumentProcess> DynamicPluginDocumentProcesses { get; set; }

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
}
