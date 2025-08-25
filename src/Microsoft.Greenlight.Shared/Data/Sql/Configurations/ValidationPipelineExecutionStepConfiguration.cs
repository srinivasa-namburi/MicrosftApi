// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.Validation;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for ValidationPipelineExecutionStep entity.
/// </summary>
public sealed class ValidationPipelineExecutionStepConfiguration : IEntityTypeConfiguration<ValidationPipelineExecutionStep>
{
    public void Configure(EntityTypeBuilder<ValidationPipelineExecutionStep> builder)
    {
        builder.ToTable("ValidationPipelineExecutionSteps");

        builder.HasIndex(e => new { e.ValidationPipelineExecutionId, e.Order });

        builder.HasOne(e => e.ValidationPipelineExecution)
            .WithMany(e => e.ExecutionSteps)
            .HasForeignKey(e => e.ValidationPipelineExecutionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ValidationExecutionStepContentNodeResults)
            .WithOne(e => e.ValidationPipelineExecutionStep)
            .HasForeignKey(e => e.ValidationPipelineExecutionStepId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
    }
}
