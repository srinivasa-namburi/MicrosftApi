// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for DocumentLibraryFileStorageSource entity.
/// </summary>
public sealed class DocumentLibraryFileStorageSourceConfiguration : IEntityTypeConfiguration<DocumentLibraryFileStorageSource>
{
    public void Configure(EntityTypeBuilder<DocumentLibraryFileStorageSource> builder)
    {
        builder.ToTable("DocumentLibraryFileStorageSources");

        builder.HasIndex(e => new { e.DocumentLibraryId, e.FileStorageSourceId }).IsUnique();

        builder.HasOne(e => e.DocumentLibrary)
            .WithMany(d => d.FileStorageSources)
            .HasForeignKey(e => e.DocumentLibraryId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.FileStorageSource)
            .WithMany(f => f.DocumentLibrarySources)
            .HasForeignKey(e => e.FileStorageSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}