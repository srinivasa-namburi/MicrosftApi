// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for IngestedDocumentFileAcknowledgment join entity.
/// </summary>
public sealed class IngestedDocumentFileAcknowledgmentConfiguration : IEntityTypeConfiguration<IngestedDocumentFileAcknowledgment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IngestedDocumentFileAcknowledgment> builder)
    {
        builder.ToTable("IngestedDocumentFileAcknowledgments");

        builder.HasIndex(x => new { x.IngestedDocumentId, x.FileAcknowledgmentRecordId })
            .IsUnique();

        builder.HasOne(x => x.IngestedDocument)
            .WithMany(x => x.IngestedDocumentFileAcknowledgments)
            .HasForeignKey(x => x.IngestedDocumentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FileAcknowledgmentRecord)
            .WithMany(x => x.IngestedDocumentLinks)
            .HasForeignKey(x => x.FileAcknowledgmentRecordId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
