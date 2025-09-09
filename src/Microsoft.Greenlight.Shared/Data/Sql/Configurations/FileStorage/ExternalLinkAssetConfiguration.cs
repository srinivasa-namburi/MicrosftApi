// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations.FileStorage;

/// <summary>
/// Configuration for the ExternalLinkAsset entity.
/// </summary>
public class ExternalLinkAssetConfiguration : IEntityTypeConfiguration<ExternalLinkAsset>
{
    /// <summary>
    /// Configures the ExternalLinkAsset entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<ExternalLinkAsset> builder)
    {
        builder.ToTable("ExternalLinkAssets");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(e => e.MimeType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.FileHash)
            .HasMaxLength(64);

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(e => e.FileSize)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1024);

        builder.Property(e => e.FileStorageSourceId)
            .IsRequired(false);

        // Add index on FileHash for deduplication queries
        builder.HasIndex(e => e.FileHash)
            .HasDatabaseName("IX_ExternalLinkAssets_FileHash");

        // Add index on Url for lookup performance
        builder.HasIndex(e => e.Url)
            .HasDatabaseName("IX_ExternalLinkAssets_Url");

        // Add index on FileStorageSourceId for efficient lookups
        builder.HasIndex(e => e.FileStorageSourceId)
            .HasDatabaseName("IX_ExternalLinkAssets_FileStorageSourceId");

        // Configure relationship to FileStorageSource
        builder.HasOne(e => e.FileStorageSource)
            .WithMany()
            .HasForeignKey(e => e.FileStorageSourceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}