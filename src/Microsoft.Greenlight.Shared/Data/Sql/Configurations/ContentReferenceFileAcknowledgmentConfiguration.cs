// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for ContentReferenceFileAcknowledgment join entity.
/// </summary>
public sealed class ContentReferenceFileAcknowledgmentConfiguration : IEntityTypeConfiguration<ContentReferenceFileAcknowledgment>
{
    public void Configure(EntityTypeBuilder<ContentReferenceFileAcknowledgment> builder)
    {
        builder.ToTable("ContentReferenceFileAcknowledgments");

        builder.HasIndex(x => new { x.ContentReferenceItemId, x.FileAcknowledgmentRecordId })
            .IsUnique();

        builder.HasOne(x => x.ContentReferenceItem)
            .WithMany()
            .HasForeignKey(x => x.ContentReferenceItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(x => x.FileAcknowledgmentRecord)
            .WithMany()
            .HasForeignKey(x => x.FileAcknowledgmentRecordId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}

