// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.Validation;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for ValidationExecutionStepContentNodeResult entity.
/// </summary>
public sealed class ValidationExecutionStepContentNodeResultConfiguration : IEntityTypeConfiguration<ValidationExecutionStepContentNodeResult>
{
    public void Configure(EntityTypeBuilder<ValidationExecutionStepContentNodeResult> builder)
    {
        builder.ToTable("ValidationExecutionStepContentNodeResults");

        builder.HasIndex(e => e.ValidationPipelineExecutionStepResultId).IsUnique(false);
        builder.HasIndex(e => e.OriginalContentNodeId).IsUnique(false);
        builder.HasIndex(e => new { e.OriginalContentNodeId, e.ResultantContentNodeId }).IsUnique(false);
        builder.HasIndex(e => e.ApplicationStatus).IsUnique(false);

        builder.HasOne(e => e.OriginalContentNode)
            .WithMany()
            .HasForeignKey(e => e.OriginalContentNodeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(e => e.ResultantContentNode)
            .WithMany()
            .HasForeignKey(e => e.ResultantContentNodeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
