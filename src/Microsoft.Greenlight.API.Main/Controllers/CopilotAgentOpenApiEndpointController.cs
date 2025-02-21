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

            var requestPort = Request.Host.Port;
            if (requestPort.HasValue && (requestPort != 443 || requestPort != 80))
            {
                swaggerDoc.Servers.Add(new OpenApiServer()
                {
                    Url = $"{Request.Scheme}://{Request.Host.Host}:{requestPort}"
                });
            }
            else
            {
                swaggerDoc.Servers.Add(new OpenApiServer()
                {
                    Url = $"{Request.Scheme}://{Request.Host.Host}"
                });
            }


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

            // --------------------------------------------
            // 1. Collect references appearing in the path ops
            // --------------------------------------------
            foreach (var pathItem in swaggerDoc.Paths.Values)
            {
                foreach (var operation in pathItem.Operations.Values)
                {
                    // Collect parameter references
                    foreach (var parameter in operation.Parameters)
                    {
                        if (parameter.Schema != null)
                        {
                            CollectSchemaReferences(parameter.Schema, usedSchemas, swaggerDoc.Components?.Schemas);
                        }
                    }

                    // Collect request body references
                    if (operation.RequestBody != null)
                    {
                        foreach (var media in operation.RequestBody.Content.Values)
                        {
                            if (media.Schema != null)
                            {
                                CollectSchemaReferences(media.Schema, usedSchemas, swaggerDoc.Components?.Schemas);
                            }
                        }
                    }

                    // Collect response references
                    foreach (var response in operation.Responses.Values)
                    {
                        foreach (var media in response.Content.Values)
                        {
                            if (media.Schema != null)
                            {
                                CollectSchemaReferences(media.Schema, usedSchemas, swaggerDoc.Components?.Schemas);
                            }
                        }
                    }
                }
            }

            // --------------------------------------------
            // 2. Recursively collect references from each used schema
            //    until there are no more new references
            // --------------------------------------------
            if (swaggerDoc.Components?.Schemas == null)
                return;

            bool foundNew;
            do
            {
                foundNew = false;
                var currentList = usedSchemas.ToList();

                foreach (var key in currentList)
                {
                    if (swaggerDoc.Components.Schemas.TryGetValue(key, out var subSchema))
                    {
                        var beforeCount = usedSchemas.Count;
                        CollectSchemaReferences(subSchema, usedSchemas, swaggerDoc.Components.Schemas);
                        if (usedSchemas.Count > beforeCount) foundNew = true;
                    }
                }
            }
            while (foundNew);

            // --------------------------------------------
            // 3. Remove anything that isn't actually used
            // --------------------------------------------
            var unusedSchemaKeys = swaggerDoc.Components.Schemas.Keys
                .Where(schemaName => !usedSchemas.Contains(schemaName))
                .ToArray();

            foreach (var key in unusedSchemaKeys)
            {
                swaggerDoc.Components.Schemas.Remove(key);
            }
        }


        private static void CollectSchemaReferences(OpenApiSchema schema, HashSet<string> usedSchemas, IDictionary<string, OpenApiSchema>? allSchemas)
        {
            if (schema == null)
            {
                return;
            }

            // Handle direct reference
            if (schema.Reference != null && !string.IsNullOrEmpty(schema.Reference.Id))
            {
                usedSchemas.Add(schema.Reference.Id);

                // If this is an enum schema, we need to include it
                if (allSchemas?.TryGetValue(schema.Reference.Id, out var referencedSchema) == true)
                {
                    if (referencedSchema.Enum?.Any() == true)
                    {
                        usedSchemas.Add(schema.Reference.Id);
                    }
                }
            }

            // Handle properties
            if (schema.Properties != null)
            {
                foreach (var prop in schema.Properties)
                {
                    CollectSchemaReferences(prop.Value, usedSchemas, allSchemas);
                }
            }

            // Handle additional properties
            if (schema.AdditionalProperties != null)
            {
                CollectSchemaReferences(schema.AdditionalProperties, usedSchemas, allSchemas);
            }

            // Handle array items
            if (schema.Items != null)
            {
                CollectSchemaReferences(schema.Items, usedSchemas, allSchemas);
            }

            // Handle allOf
            if (schema.AllOf != null)
            {
                foreach (var subSchema in schema.AllOf)
                {
                    CollectSchemaReferences(subSchema, usedSchemas, allSchemas);
                }
            }

            // Handle anyOf
            if (schema.AnyOf != null)
            {
                foreach (var subSchema in schema.AnyOf)
                {
                    CollectSchemaReferences(subSchema, usedSchemas, allSchemas);
                }
            }

            // Handle oneOf
            if (schema.OneOf != null)
            {
                foreach (var subSchema in schema.OneOf)
                {
                    CollectSchemaReferences(subSchema, usedSchemas, allSchemas);
                }
            }
        }




    }
}
