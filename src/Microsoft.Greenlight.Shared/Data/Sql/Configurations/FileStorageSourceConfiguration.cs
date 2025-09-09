// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for FileStorageSource entity.
/// </summary>
public sealed class FileStorageSourceConfiguration : IEntityTypeConfiguration<FileStorageSource>
{
    public void Configure(EntityTypeBuilder<FileStorageSource> builder)
    {
        builder.ToTable("FileStorageSources");

        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.ContainerOrPath).IsRequired();
        builder.Property(e => e.ShouldMoveFiles).HasDefaultValue(false);
        builder.Property(e => e.FileStorageHostId).IsRequired();

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => new { e.FileStorageHostId, e.ContainerOrPath }).IsUnique();

        builder.HasOne(e => e.FileStorageHost)
            .WithMany(e => e.Sources)
            .HasForeignKey(e => e.FileStorageHostId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.DocumentProcessSources)
            .WithOne(e => e.FileStorageSource)
            .HasForeignKey(e => e.FileStorageSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.DocumentLibrarySources)
            .WithOne(e => e.FileStorageSource)
            .HasForeignKey(e => e.FileStorageSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}