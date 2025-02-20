using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.API.Main.Controllers
{
    /// <summary>
    /// The CopilotAgentOpenApiEndpointController is a special controller that generates
    /// OpenAPI documents for a specific domain group. This allows CoPilot users to interact
    /// with the domain group's document processes using a single endpoint.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/copilot-agent")]
    [ApiController]
    public class CopilotAgentOpenApiEndpointController : ControllerBase
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly ISwaggerProvider _swaggerProvider;
        private readonly IMemoryCache _cache;

        /// <inheritdoc />
        public CopilotAgentOpenApiEndpointController(
            DocGenerationDbContext dbContext,
            ISwaggerProvider swaggerProvider,
            IMemoryCache cache)
        {
            _dbContext = dbContext;
            _swaggerProvider = swaggerProvider;
            _cache = cache;
        }

        /// <summary>
        /// Returns the OpenAPI document for a domain group that only includes endpoints from the CopilotAgentController.
        /// A trailing "no-cache" segment in the route will bypass the caching mechanism.
        /// </summary>
        /// <param name="domainGroupId">The ID for the domain group</param>
        /// <param name="cache">Optional - set to "no-cache" to override caching</param>
        /// <returns></returns>
        [HttpGet("{domainGroupId:guid}/openapi.json/{cache?}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("application/json")]
        public async Task<IActionResult> GetDomainGroupOpenApi(Guid domainGroupId, string? cache)
        {
            // Validate that the DomainGroup exists and is allowed to be exposed.
            var domainGroup = await _dbContext.DomainGroups
                .FirstOrDefaultAsync(d => d.Id == domainGroupId);

            if (domainGroup == null || !domainGroup.ExposeCoPilotAgentEndpoint)
            {
                return NotFound();
            }

            bool bypassCache = string.Equals(cache, "no-cache", StringComparison.OrdinalIgnoreCase);
            string cacheKey = $"DomainGroupOpenApi_{domainGroupId}";

            if (!bypassCache && _cache.TryGetValue(cacheKey, out OpenApiDocument? cachedDoc))
            {
                if (cachedDoc != null)
                {
                    return Content(SerializeSwaggerDoc(cachedDoc), "application/json");
                }
            }

            // Generate the base Swagger document. "v1" must match your Swagger configuration version.
            var swaggerDoc = _swaggerProvider.GetSwagger("v1");

            // Filter the document to include only endpoints from CopilotAgentController.
            // (Assuming the default tag for these endpoints is "CopilotAgent".)
            var modifiedPaths = new OpenApiPaths();
            foreach (var path in swaggerDoc.Paths)
            {
                bool includePath = path.Value.Operations.Any(op =>
                    op.Value.Tags.Any(tag => string.Equals(tag.Name, "CopilotAgent", StringComparison.OrdinalIgnoreCase))
                );

                if (!includePath)
                {
                    continue;
                }

                // Replace the {domainGroupId} placeholder with the actual id.
                var newPathKey = path.Key.Replace("{domainGroupId}", domainGroupId.ToString());

                // Remove the domainGroupId parameter from operations.
                var newPathItem = path.Value;
                foreach (var operation in newPathItem.Operations)
                {
                    operation.Value.Parameters = operation.Value.Parameters
                        .Where(p => p.Name != "domainGroupId")
                        .ToList();
                }

                modifiedPaths.Add(newPathKey, newPathItem);
            }
            swaggerDoc.Paths = modifiedPaths;

            // Remove unused schemas from the components. Only schemas referenced in the remaining paths are kept.
            RemoveUnusedSchemas(swaggerDoc);

            // Set the title
            swaggerDoc.Info.Title = "Domain Expert CoPilot : " + domainGroup.Name;
            swaggerDoc.Info.Description = domainGroup.Description;

            // Cache the generated document for 5 minutes.
            if (!bypassCache)
            {
                _cache.Set(cacheKey, swaggerDoc, TimeSpan.FromMinutes(5));
            }

            // Manually serialize the OpenApiDocument to JSON.
            var json = SerializeSwaggerDoc(swaggerDoc);
            return Content(json, "application/json");
        }

        private static string SerializeSwaggerDoc(OpenApiDocument swaggerDoc)
        {
            using var stringWriter = new StringWriter();
            var jsonWriter = new OpenApiJsonWriter(stringWriter);
            swaggerDoc.SerializeAsV3(jsonWriter);
            jsonWriter.Flush();
            return stringWriter.ToString();
        }

        /// <summary>
        /// Removes any schemas from the document's components that are not referenced
        /// in any operation (parameters, request bodies, responses) in the paths.
        /// </summary>
        private static void RemoveUnusedSchemas(OpenApiDocument swaggerDoc)
        {
            var usedSchemas = new HashSet<string>();

            // Collect references from all operations in all paths.
            foreach (var pathItem in swaggerDoc.Paths.Values)
            {
                foreach (var operation in pathItem.Operations.Values)
                {
                    // Check parameters
                    foreach (var parameter in operation.Parameters)
                    {
                        if (parameter.Schema != null)
                        {
                            CollectSchemaReferences(parameter.Schema, usedSchemas);
                        }
                    }
                    // Check request bodies
                    if (operation.RequestBody != null)
                    {
                        foreach (var media in operation.RequestBody.Content.Values)
                        {
                            if (media.Schema != null)
                            {
                                CollectSchemaReferences(media.Schema, usedSchemas);
                            }
                        }
                    }
                    // Check responses
                    foreach (var response in operation.Responses.Values)
                    {
                        foreach (var media in response.Content.Values)
                        {
                            if (media.Schema != null)
                            {
                                CollectSchemaReferences(media.Schema, usedSchemas);
                            }
                        }
                    }
                }
            }

            // Remove schemas that are not referenced.
            var keysToRemove = swaggerDoc.Components.Schemas
                .Where(kvp => !usedSchemas.Contains(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                swaggerDoc.Components.Schemas.Remove(key);
            }
        }

        /// <summary>
        /// Recursively collects schema references from the provided schema.
        /// </summary>
        private static void CollectSchemaReferences(OpenApiSchema schema, HashSet<string> usedSchemas)
        {
            if (schema == null)
            {
                return;
            }

            if (schema.Reference != null && !string.IsNullOrEmpty(schema.Reference.Id))
            {
                usedSchemas.Add(schema.Reference.Id);
            }

            if (schema.Properties != null)
            {
                foreach (var prop in schema.Properties.Values)
                {
                    CollectSchemaReferences(prop, usedSchemas);
                }
            }

            if (schema.AdditionalProperties != null)
            {
                CollectSchemaReferences(schema.AdditionalProperties, usedSchemas);
            }

            if (schema.Items != null)
            {
                CollectSchemaReferences(schema.Items, usedSchemas);
            }

            if (schema.AllOf != null)
            {
                foreach (var subSchema in schema.AllOf)
                {
                    CollectSchemaReferences(subSchema, usedSchemas);
                }
            }

            if (schema.AnyOf != null)
            {
                foreach (var subSchema in schema.AnyOf)
                {
                    CollectSchemaReferences(subSchema, usedSchemas);
                }
            }

            if (schema.OneOf != null)
            {
                foreach (var subSchema in schema.OneOf)
                {
                    CollectSchemaReferences(subSchema, usedSchemas);
                }
            }
        }
    }
}
