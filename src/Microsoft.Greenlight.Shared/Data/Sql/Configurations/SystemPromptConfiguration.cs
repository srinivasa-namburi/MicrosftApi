// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.Configuration;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for SystemPrompt entity.
/// </summary>
public sealed class SystemPromptConfiguration : IEntityTypeConfiguration<SystemPrompt>
{
    /// <summary>
    /// Configures the SystemPrompt entity for EF Core.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<SystemPrompt> builder)
    {
        builder.ToTable("SystemPrompts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.Name });
    }
}
