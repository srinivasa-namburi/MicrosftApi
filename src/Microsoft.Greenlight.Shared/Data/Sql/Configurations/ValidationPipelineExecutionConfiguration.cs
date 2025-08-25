// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.Validation;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for ValidationPipelineExecution entity.
/// </summary>
public sealed class ValidationPipelineExecutionConfiguration : IEntityTypeConfiguration<ValidationPipelineExecution>
{
    public void Configure(EntityTypeBuilder<ValidationPipelineExecution> builder)
    {
        builder.ToTable("ValidationPipelineExecutions");

        builder.HasIndex(e => e.DocumentProcessValidationPipelineId)
            .IsUnique(false);

        builder.HasOne(e => e.DocumentProcessValidationPipeline)
            .WithMany(e => e.ValidationPipelineExecutions)
            .HasForeignKey(e => e.DocumentProcessValidationPipelineId)
            .IsRequired();

        builder.HasMany(e => e.ExecutionSteps)
            .WithOne(e => e.ValidationPipelineExecution)
            .HasForeignKey(e => e.ValidationPipelineExecutionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.GeneratedDocument)
            .WithMany(e => e.ValidationPipelineExecutions)
            .HasForeignKey(e => e.GeneratedDocumentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
