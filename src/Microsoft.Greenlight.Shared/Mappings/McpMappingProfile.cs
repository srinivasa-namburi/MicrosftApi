// Copyright (c) Microsoft Corporation. All rights reserved.
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets;
using Microsoft.Greenlight.Shared.Models.Configuration;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// AutoMapper profile for MCP-related mappings.
/// </summary>
public sealed class McpMappingProfile : Profile
{
    public McpMappingProfile()
    {
        CreateMap<McpSecret, McpSecretInfo>();
    }
}

