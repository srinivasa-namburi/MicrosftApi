// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for FileAcknowledgmentRecord entity.
/// </summary>
public sealed class FileAcknowledgmentRecordConfiguration : IEntityTypeConfiguration<FileAcknowledgmentRecord>
{
    /// <summary>
    /// Configures the EF Core model for FileAcknowledgmentRecord.
    /// </summary>
    public void Configure(EntityTypeBuilder<FileAcknowledgmentRecord> builder)
    {
        builder.ToTable("FileAcknowledgmentRecords");

        builder.Property(e => e.RelativeFilePath).IsRequired();
        builder.Property(e => e.FileStorageSourceInternalUrl).IsRequired();

        builder.HasIndex(e => new { e.FileStorageSourceId, e.RelativeFilePath }).IsUnique();

        builder.HasOne(e => e.FileStorageSource)
            .WithMany()
            .HasForeignKey(e => e.FileStorageSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // One FileAcknowledgmentRecord can be linked to many IngestedDocuments via join entity
        builder.HasMany(e => e.IngestedDocumentLinks)
            .WithOne(link => link.FileAcknowledgmentRecord)
            .HasForeignKey(link => link.FileAcknowledgmentRecordId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}