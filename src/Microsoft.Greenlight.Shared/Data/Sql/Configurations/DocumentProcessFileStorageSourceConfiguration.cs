// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for DocumentProcessFileStorageSource entity.
/// </summary>
public sealed class DocumentProcessFileStorageSourceConfiguration : IEntityTypeConfiguration<DocumentProcessFileStorageSource>
{
    public void Configure(EntityTypeBuilder<DocumentProcessFileStorageSource> builder)
    {
        builder.ToTable("DocumentProcessFileStorageSources");

        builder.HasIndex(e => new { e.DocumentProcessId, e.FileStorageSourceId }).IsUnique();

        builder.HasOne(e => e.DocumentProcess)
            .WithMany()
            .HasForeignKey(e => e.DocumentProcessId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.FileStorageSource)
            .WithMany(f => f.DocumentProcessSources)
            .HasForeignKey(e => e.FileStorageSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}