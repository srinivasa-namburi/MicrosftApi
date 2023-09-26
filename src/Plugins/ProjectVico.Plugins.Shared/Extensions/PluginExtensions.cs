// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectVico.Plugins.Shared.Extensions;

public static class PluginExtensions
{
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        IConfiguration configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        string[] allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (allowedOrigins.Length > 0)
        {
            //services.AddCors(options =>
            //{
            //    options.AddDefaultPolicy(
            //        policy =>
            //        {
            //            policy.WithOrigins(allowedOrigins)
            //                .WithMethods("GET", "POST", "DELETE")
            //                .AllowAnyHeader();
            //        });
            //});
        }

        return services;
    }
}
