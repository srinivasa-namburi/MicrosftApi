// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Greenlight.Shared.Models.Configuration;

namespace Microsoft.Greenlight.Shared.Data.Sql.Configurations;

/// <summary>
/// EF Core configuration for McpSecret entity.
/// </summary>
public sealed class McpSecretConfiguration : IEntityTypeConfiguration<McpSecret>
{
    public void Configure(EntityTypeBuilder<McpSecret> builder)
    {
        builder.ToTable("McpSecrets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SecretHash).IsRequired();
        builder.Property(x => x.SecretSalt).IsRequired();
        builder.Property(x => x.UserOid).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.LastUsedUtc).IsRequired(false);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.Name });
    }
}

