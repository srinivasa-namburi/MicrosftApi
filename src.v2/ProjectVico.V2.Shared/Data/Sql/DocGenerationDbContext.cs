using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.SagaState;

namespace ProjectVico.V2.Shared.Data.Sql;

public class DocGenerationDbContext : DbContext
{
    private readonly DbContextOptions<DocGenerationDbContext> _dbContextOptions;

    public DocGenerationDbContext(DbContextOptions<DocGenerationDbContext> dbContextOptions)
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
        // JSON property to hold metadata, for now just the DocumentGenerationRequest, but this will be generalized later
        modelBuilder.Entity<DocumentGenerationSagaState>()
            .Property<DocumentGenerationRequest>(x => x.DocumentGenerationRequest)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<DocumentGenerationRequest>(v, JsonSerializerOptions.Default)!
            );

        // Mass Transit SAGA for Document Ingestion
        modelBuilder.Entity<DocumentIngestionSagaState>()
            .HasKey(x => x.CorrelationId);
    }

    public DbSet<DocumentGenerationSagaState> DocumentGenerationSagaStates { get; set; } = null!;
    public DbSet<DocumentIngestionSagaState> DocumentIngestionSagaStates { get; set; } = null!;
    public DbSet<GeneratedDocument> GeneratedDocuments { get; set; }
    public DbSet<IngestedDocument> IngestedDocuments { get; set; }

    public DbSet<ContentNode> ContentNodes { get; set; }

    public DbSet<Table> Tables { get; set; }
    public DbSet<TableCell> TableCells { get; set; }
    public DbSet<BoundingRegion> BoundingRegions { get; set; }
    public DbSet<BoundingPolygon> BoundingPolygons { get; set; }

}