// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for FileStorageHost entity.
/// </summary>
public sealed class FileStorageHostConfiguration : IEntityTypeConfiguration<FileStorageHost>
{
    public void Configure(EntityTypeBuilder<FileStorageHost> builder)
    {
        builder.ToTable("FileStorageHosts");

        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.ConnectionString).IsRequired();

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => new { e.ProviderType, e.ConnectionString }).IsUnique(false);

        builder.HasMany(e => e.Sources)
            .WithOne(e => e.FileStorageHost)
            .HasForeignKey(e => e.FileStorageHostId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}