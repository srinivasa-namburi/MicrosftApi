// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.Validation;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for ValidationPipelineExecutionStepResult entity.
/// </summary>
public sealed class ValidationPipelineExecutionStepResultConfiguration : IEntityTypeConfiguration<ValidationPipelineExecutionStepResult>
{
    public void Configure(EntityTypeBuilder<ValidationPipelineExecutionStepResult> builder)
    {
        builder.ToTable("ValidationPipelineExecutionStepResults");

        builder.HasIndex(e => e.ValidationPipelineExecutionStepId).IsUnique();

        builder.HasOne(e => e.ValidationPipelineExecutionStep)
            .WithOne(e => e.ValidationPipelineExecutionStepResult)
            .HasForeignKey<ValidationPipelineExecutionStepResult>(e => e.ValidationPipelineExecutionStepId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ContentNodeResults)
            .WithOne(e => e.ValidationPipelineExecutionStepResult)
            .HasForeignKey(e => e.ValidationPipelineExecutionStepResultId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
