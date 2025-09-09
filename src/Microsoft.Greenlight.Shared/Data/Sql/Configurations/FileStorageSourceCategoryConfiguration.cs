// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for <see cref="FileStorageSourceCategory"/> entity.
/// </summary>
public sealed class FileStorageSourceCategoryConfiguration : IEntityTypeConfiguration<FileStorageSourceCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileStorageSourceCategory> builder)
    {
        builder.ToTable("FileStorageSourceCategories");

        builder.Property(e => e.FileStorageSourceId).IsRequired();
        builder.Property(e => e.DataType).IsRequired();

        // Prevent duplicate category assignment for the same source
        builder.HasIndex(e => new { e.FileStorageSourceId, e.DataType }).IsUnique();

        builder.HasOne(e => e.FileStorageSource)
            .WithMany(e => e.Categories)
            .HasForeignKey(e => e.FileStorageSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
