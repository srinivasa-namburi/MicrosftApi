using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for ContentReferenceVectorDocument entity.
/// </summary>
public sealed class ContentReferenceVectorDocumentConfiguration : IEntityTypeConfiguration<ContentReferenceVectorDocument>
{
    public void Configure(EntityTypeBuilder<ContentReferenceVectorDocument> builder)
    {
        builder.ToTable("ContentReferenceVectorDocuments");

        builder.HasIndex(e => new { e.ContentReferenceItemId, e.VectorStoreIndexName })
            .IsUnique();

        builder.Property(e => e.VectorStoreIndexName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.VectorStoreDocumentId)
            .HasMaxLength(256)
            .IsRequired();
    }
}

