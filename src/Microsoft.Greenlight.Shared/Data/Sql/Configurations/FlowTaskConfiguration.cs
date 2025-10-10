// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.FlowTasks;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for FlowTaskTemplate entity.
/// </summary>
public sealed class FlowTaskTemplateConfiguration : IEntityTypeConfiguration<FlowTaskTemplate>
{
    public void Configure(EntityTypeBuilder<FlowTaskTemplate> builder)
    {
        builder.ToTable("FlowTaskTemplates");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Category).IsRequired().HasMaxLength(50);
        builder.Property(e => e.InitialPrompt).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.IsActive);

        builder.HasMany(e => e.Sections)
            .WithOne(e => e.FlowTaskTemplate)
            .HasForeignKey(e => e.FlowTaskTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.OutputTemplates)
            .WithOne(e => e.FlowTaskTemplate)
            .HasForeignKey(e => e.FlowTaskTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Note: DataSources relationship is configured in DocGenerationDbContext.OnModelCreating
        // due to TPH (Table Per Hierarchy) requirements
    }
}

/// <summary>
/// EF Core configuration for FlowTaskSection entity.
/// </summary>
public sealed class FlowTaskSectionConfiguration : IEntityTypeConfiguration<FlowTaskSection>
{
    public void Configure(EntityTypeBuilder<FlowTaskSection> builder)
    {
        builder.ToTable("FlowTaskSections");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.IsRequired).HasDefaultValue(true);

        builder.HasIndex(e => e.FlowTaskTemplateId);
        builder.HasIndex(e => new { e.FlowTaskTemplateId, e.SortOrder });

        builder.HasOne(e => e.FlowTaskTemplate)
            .WithMany(e => e.Sections)
            .HasForeignKey(e => e.FlowTaskTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Requirements)
            .WithOne(e => e.FlowTaskSection)
            .HasForeignKey(e => e.FlowTaskSectionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// EF Core configuration for FlowTaskRequirement entity.
/// </summary>
public sealed class FlowTaskRequirementConfiguration : IEntityTypeConfiguration<FlowTaskRequirement>
{
    public void Configure(EntityTypeBuilder<FlowTaskRequirement> builder)
    {
        builder.ToTable("FlowTaskRequirements");

        builder.Property(e => e.FieldName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.DataType).IsRequired().HasMaxLength(50).HasDefaultValue("text");
        builder.Property(e => e.IsRequired).HasDefaultValue(true);

        builder.HasIndex(e => e.FlowTaskSectionId);
        builder.HasIndex(e => new { e.FlowTaskSectionId, e.FieldName });
        builder.HasIndex(e => e.DataSourceId);

        builder.HasOne(e => e.FlowTaskSection)
            .WithMany(e => e.Requirements)
            .HasForeignKey(e => e.FlowTaskSectionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.DataSource)
            .WithMany()
            .HasForeignKey(e => e.DataSourceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// Note: FlowTaskDataSource TPH configuration is in DocGenerationDbContext.OnModelCreating
// following the SourceReferenceItem pattern

/// <summary>
/// EF Core configuration for FlowTaskMcpToolParameter entity.
/// </summary>
public sealed class FlowTaskMcpToolParameterConfiguration : IEntityTypeConfiguration<FlowTaskMcpToolParameter>
{
    public void Configure(EntityTypeBuilder<FlowTaskMcpToolParameter> builder)
    {
        builder.ToTable("FlowTaskMcpToolParameters");

        builder.Property(e => e.ParameterName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ParameterValue).IsRequired();
        builder.Property(e => e.DataType).HasMaxLength(50);

        builder.HasIndex(e => e.FlowTaskDataSourceId);
        builder.HasIndex(e => new { e.FlowTaskDataSourceId, e.ParameterName });

        // Configure relationship: FK references base type FlowTaskDataSource.Id
        builder.HasOne(e => e.FlowTaskMcpToolDataSource)
            .WithMany(ds => ds.Parameters)
            .HasForeignKey(e => e.FlowTaskDataSourceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// EF Core configuration for FlowTaskOutputTemplate entity.
/// </summary>
public sealed class FlowTaskOutputTemplateConfiguration : IEntityTypeConfiguration<FlowTaskOutputTemplate>
{
    public void Configure(EntityTypeBuilder<FlowTaskOutputTemplate> builder)
    {
        builder.ToTable("FlowTaskOutputTemplates");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.OutputType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.TemplateContent).IsRequired();
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.McpToolName).HasMaxLength(200);

        builder.HasIndex(e => e.FlowTaskTemplateId);
        builder.HasIndex(e => new { e.FlowTaskTemplateId, e.Name });

        builder.HasOne(e => e.FlowTaskTemplate)
            .WithMany(e => e.OutputTemplates)
            .HasForeignKey(e => e.FlowTaskTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
