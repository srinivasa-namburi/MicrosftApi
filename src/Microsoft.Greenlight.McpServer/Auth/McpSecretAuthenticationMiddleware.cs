// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.McpServer.Monitoring;
using Microsoft.Greenlight.McpServer.Options;

namespace Microsoft.Greenlight.McpServer.Auth;

/// <summary>
/// Middleware that enables secret-based authentication for the MCP server.
/// When enabled via configuration, validates a configured header containing the secret
/// and establishes an authenticated principal with the user OID bound directly to the API secret.
/// </summary>
public sealed class McpSecretAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpSecretAuthenticationMiddleware> _logger;
    private const string DefaultSecretHeaderName = "X-MCP-Secret";

    /// <summary>
    /// Initializes a new instance of the <see cref="McpSecretAuthenticationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    public McpSecretAuthenticationMiddleware(RequestDelegate next, ILogger<McpSecretAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="configuration">Application configuration to read runtime options.</param>
    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration,
        IOptionsSnapshot<McpOptions> optionsSnapshot,
        Microsoft.EntityFrameworkCore.IDbContextFactory<Microsoft.Greenlight.Shared.Data.Sql.DocGenerationDbContext> dbContextFactory,
        Microsoft.Greenlight.Shared.Services.Security.ISecretHashingService hashing)
    {
        // Scope only to /mcp* routes
        var path = context.Request.Path.HasValue ? context.Request.Path.Value : string.Empty;
        if (!string.IsNullOrEmpty(path) && path!.StartsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            // If already authenticated (e.g., via JWT), do not override
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                var opts = optionsSnapshot.Value;
                var secretEnabled = opts.SecretEnabled;
                if (secretEnabled)
                {
                    var secretHeaderName = opts.SecretHeaderName;
                    if (string.IsNullOrWhiteSpace(secretHeaderName))
                    {
                        secretHeaderName = DefaultSecretHeaderName;
                    }


                    // Validate secret header
                    if (context.Request.Headers.TryGetValue(secretHeaderName, out var provided) && !string.IsNullOrEmpty(provided))
                    {
                        var disableAuth = opts.DisableAuth;

                        // Validate against active secrets from the database (hashed)
                        string? matchedUserOid = null;
                        string? matchedSecretName = null;
                        await using var db = await dbContextFactory.CreateDbContextAsync(context.RequestAborted);
                        var candidates = db.Set<Microsoft.Greenlight.Shared.Models.Configuration.McpSecret>().Where(s => s.IsActive).ToList();
                        foreach (var s in candidates)
                        {
                            if (hashing.Verify(provided.ToString()!, s.SecretSalt, s.SecretHash))
                            {
                                matchedUserOid = s.UserOid;
                                matchedSecretName = s.Name;
                                s.LastUsedUtc = DateTime.UtcNow;
                                // Fire and forget save of last used
                                _ = db.SaveChangesAsync(context.RequestAborted);
                                break;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(matchedUserOid))
                        {
                            // Use the user OID bound directly to the matched API secret
                            var impersonatedUserOid = matchedUserOid;

                            // Build identity and principal
                            var claims = new List<Claim>
                            {
                                new Claim("mcp_auth", "secret"),
                                new Claim(ClaimTypes.Name, "mcp-secret")
                            };

                            if (!string.IsNullOrWhiteSpace(impersonatedUserOid))
                            {
                                claims.Add(new Claim("oid", impersonatedUserOid));
                            }

                            if (!string.IsNullOrWhiteSpace(matchedSecretName))
                            {
                                claims.Add(new Claim("mcp_secret_name", matchedSecretName));
                            }

                            var identity = new ClaimsIdentity(claims, authenticationType: "McpSecret");
                            var principal = new ClaimsPrincipal(identity);

                            // If DisableAuth is set, we still establish identity for downstream tools to obtain OID.
                            context.User = principal;

                            _logger.LogInformation(
                                "MCP secret-based authentication succeeded (Secret='{SecretName}', HasOid={HasOid}, DisableAuth={DisableAuth}).",
                                matchedSecretName ?? string.Empty,
                                !string.IsNullOrWhiteSpace(impersonatedUserOid),
                                disableAuth);

                            McpMetrics.SecretAuthSuccess.Add(1);
                        }
                        else
                        {
                            // If a secret was provided but invalid, reject with 401 for /mcp routes
                            _logger.LogWarning("MCP secret provided but invalid.");
                            McpMetrics.SecretAuthFailure.Add(1);
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsync("Invalid MCP secret.");
                            return;
                        }
                    }
                    else
                    {
                        // Secret required but not provided and no JWT; reject
                        _logger.LogWarning("MCP secret required but missing.");
                        McpMetrics.SecretAuthFailure.Add(1);
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("MCP secret required.");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
