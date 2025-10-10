// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Grains.Chat.Services;

namespace Microsoft.Greenlight.Grains.Chat.Extensions;

/// <summary>
/// Chat grain specific service registrations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the chat grain assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGreenlightChatServices(this IServiceCollection services)
    {
        services.AddTransient<IFlowTaskTemplateResolver, FlowTaskTemplateResolver>();
        return services;
    }
}
