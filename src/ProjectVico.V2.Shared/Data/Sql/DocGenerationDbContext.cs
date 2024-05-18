using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProjectVico.V2.Shared.Models;
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
                modelBuilder.Entity(entityType.ClrType).Property(typeof(bool), nameof(EntityBase.IsActive)).HasDefaultValue(true);
                modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(EntityBase.IsActive));
                modelBuilder.Entity(entityType.ClrType).HasIndex(new string[]{nameof(EntityBase.DeletedAt), nameof(EntityBase.IsActive)});
                
                // Apply global query filter for IsActive
                var entityParam = Expression.Parameter(entityType.ClrType, "x");
                var isActiveProperty = Expression.Property(entityParam, nameof(EntityBase.IsActive));
                var lambda = Expression.Lambda(isActiveProperty, entityParam);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
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
            .HasOne(x=>x.SummarizedByConversationSummary)
            .WithMany(x=>x.SummarizedChatMessages)
            .HasForeignKey(x=>x.SummarizedByConversationSummaryId)
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
            .HasOne(x=>x.ReplyToChatMessage)
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
    public DbSet<DocumentMetadata> DocumentMetadata { get; set; }
    public DbSet<IngestedDocument> IngestedDocuments { get; set; }

    public DbSet<ContentNode> ContentNodes { get; set; }

    public DbSet<Table> Tables { get; set; }
    public DbSet<TableCell> TableCells { get; set; }
    public DbSet<BoundingRegion> BoundingRegions { get; set; }
    public DbSet<BoundingPolygon> BoundingPolygons { get; set; }
    
    public DbSet<ChatConversation> ChatConversations { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ConversationSummary> ConversationSummaries { get; set; }

    public DbSet<UserInformation> UserInformations { get; set; }

    public DbSet<DynamicDocumentProcessDefinition> DynamicDocumentProcessDefinitions { get; set; }
}