// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for IngestedDocument entity.
/// </summary>
public sealed class IngestedDocumentConfiguration : IEntityTypeConfiguration<IngestedDocument>
{
    public void Configure(EntityTypeBuilder<IngestedDocument> builder)
    {
        builder.ToTable("IngestedDocuments");

        builder.Property(x => x.RunId)
            .IsRequired();

        builder.Property(x => x.FileName)
            .IsRequired();

        builder.Property(x => x.OriginalDocumentUrl)
            .IsRequired();

        builder.Property(x => x.Container)
            .IsRequired();

        builder.Property(x => x.FolderPath)
            .IsRequired();

        builder.Property(x => x.OrchestrationId)
            .IsRequired();

        builder.HasIndex(d => d.FileHash)
            .IsUnique(false);

        builder.HasIndex(d => new { d.DocumentLibraryType, d.DocumentLibraryOrProcessName, d.Container, d.FolderPath, d.FileName, d.FileHash })
            .IsUnique();

        builder.HasIndex(d => d.OrchestrationId)
            .IsUnique(false);

        builder.HasIndex(x => new { x.DocumentLibraryType, x.DocumentLibraryOrProcessName })
            .IsUnique(false);
    }
}