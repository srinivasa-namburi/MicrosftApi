using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.DocumentProcess;
using ProjectVico.V2.Shared.Models.Review;
using ProjectVico.V2.Shared.SagaState;

namespace ProjectVico.V2.Shared.Data.Sql;

public class DocGenerationDbContext : DbContext
{
    private readonly DbContextOptions<DocGenerationDbContext> _dbContextOptions;

    public DocGenerationDbContext(
        DbContextOptions<DocGenerationDbContext> dbContextOptions
        )
        : base(dbContextOptions)
    {
        _dbContextOptions = dbContextOptions;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("ProjectVicoDB");
        }

    }

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
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

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
            .HasMany(x=>x.ReviewInstances)
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
            .HasOne(x => x.DocumentOutline)
            .WithOne(x => x.DocumentProcessDefinition)
            .HasForeignKey<DocumentOutline>(x => x.DocumentProcessDefinitionId)
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
            .HasMany(x=>x.Variables)
            .WithOne(x => x.PromptDefinition)
            .HasForeignKey(x => x.PromptDefinitionId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PromptImplementation>()
            .ToTable("PromptImplementations");

        // Index for unique combination of PromptDefinitionId and DocumentProcessDefinitionId, 
        // as a PromptDefinition can only be implemented once for a DocumentProcessDefinition
        modelBuilder.Entity<PromptImplementation>()
            .HasIndex(nameof(PromptImplementation.PromptDefinitionId), nameof(PromptImplementation.DocumentProcessDefinitionId))
            .IsUnique();

        
        modelBuilder.Entity<PromptImplementation>()
            .HasOne(x => x.DocumentProcessDefinition)
            .WithMany(x=>x.Prompts)
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

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .ToTable("DynamicDocumentProcessDefinitions");

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasIndex(nameof(DynamicDocumentProcessDefinition.ShortName))
            .IsUnique();

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .HasIndex(nameof(DynamicDocumentProcessDefinition.LogicType))
            .IsUnique(false);

        modelBuilder.Entity<DynamicDocumentProcessDefinition>()
            .Property(e => e.Repositories)
            .HasConversion(stringListToJsonConverter)
            .Metadata
                .SetValueComparer(stringListComparer);

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
            .HasIndex(nameof(ChatMessage.CreatedAt))
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
            .HasKey(x => x.CorrelationId);

        // Table per hierarchy for Document Ingestion SAGA
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasDiscriminator<string>("Discriminator")
            .HasValue<DocumentIngestionSagaState>("DocumentIngestionSagaState")
            .HasValue<KernelMemoryDocumentIngestionSagaState>("KernelMemoryDocumentIngestionSagaState");

        modelBuilder.Entity<DocumentIngestionSagaState>()
            .Property("Discriminator")
            .HasMaxLength(100);


        // Mass Transit SAGA for Document Ingestion
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasKey(x => x.CorrelationId);
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasIndex(x => x.FileHash)
            .IsUnique(false);
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasIndex(x => x.DocumentProcessName)
            .IsUnique(false);

        // Mass Transit SAGA for Kernel Memory Document Ingestion
        // The Key is the same as the base class - this is an EF Core requirement for Table per hierarchy

        modelBuilder.Entity<KernelMemoryDocumentIngestionSagaState>()
            .HasIndex(x => x.FileHash)
            .IsUnique(false);
        modelBuilder.Entity<KernelMemoryDocumentIngestionSagaState>()
            .HasIndex(x => x.DocumentProcessName)
            .IsUnique(false);

    }

    public DbSet<DocumentGenerationSagaState> DocumentGenerationSagaStates { get; set; } = null!;
    public DbSet<DocumentIngestionSagaState> DocumentIngestionSagaStates { get; set; } = null!;
    public DbSet<KernelMemoryDocumentIngestionSagaState> KernelMemoryDocumentIngestionSagaStates { get; set; } = null!;

    public DbSet<GeneratedDocument> GeneratedDocuments { get; set; }
    public DbSet<ExportedDocumentLink> ExportedDocumentLinks { get; set; }

    public DbSet<DocumentMetadata> DocumentMetadata { get; set; }
    public DbSet<ContentNode> ContentNodes { get; set; }
    
    public DbSet<IngestedDocument> IngestedDocuments { get; set; }
    
    public DbSet<Table> Tables { get; set; }
    public DbSet<TableCell> TableCells { get; set; }
    public DbSet<BoundingRegion> BoundingRegions { get; set; }
    public DbSet<BoundingPolygon> BoundingPolygons { get; set; }

    public DbSet<ChatConversation> ChatConversations { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ConversationSummary> ConversationSummaries { get; set; }

    public DbSet<UserInformation> UserInformations { get; set; }
    
    public DbSet<DynamicDocumentProcessDefinition> DynamicDocumentProcessDefinitions { get; set; }
    
    public DbSet<DocumentOutline> DocumentOutlines { get; set; }
    public DbSet<DocumentOutlineItem> DocumentOutlineItems { get; set; }
    
    public DbSet<PromptDefinition> PromptDefinitions { get; set; }
    public DbSet<PromptVariableDefinition> PromptVariableDefinitions { get; set; }
    
    public DbSet<PromptImplementation> PromptImplementations { get; set; }
    public DbSet<ReviewDefinition> ReviewDefinitions { get; set; }
    public DbSet<ReviewQuestion> ReviewQuestions { get; set; }
    public DbSet<ReviewDefinitionDocumentProcessDefinition> ReviewDefinitionDocumentProcessDefinitions { get; set; }
    public DbSet<ReviewInstance> ReviewInstances { get; set; }
    public DbSet<ReviewQuestionAnswer> ReviewQuestionAnswers { get; set; }
}