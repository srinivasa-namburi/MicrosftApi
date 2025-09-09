// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ContentReferenceTypeFileStorageSource"/> entity.
/// Configures table name, indexes and relationships.
/// </summary>
public sealed class ContentReferenceTypeFileStorageSourceConfiguration : IEntityTypeConfiguration<ContentReferenceTypeFileStorageSource>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContentReferenceTypeFileStorageSource> builder)
    {
        builder.ToTable("ContentReferenceTypeFileStorageSources");

        // Each (ContentReferenceType, FileStorageSourceId) pair must be unique
        builder.HasIndex(e => new { e.ContentReferenceType, e.FileStorageSourceId }).IsUnique();

        builder.Property(e => e.ContentReferenceType).IsRequired();
        builder.Property(e => e.FileStorageSourceId).IsRequired();
        builder.Property(e => e.Priority).HasDefaultValue(0);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.AcceptsUploads).HasDefaultValue(false);

        builder.HasOne(e => e.FileStorageSource)
            .WithMany() // No navigation collection currently on FileStorageSource
            .HasForeignKey(e => e.FileStorageSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
