using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Prompts;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Services;

/// <inheritdoc />
public class PromptDefinitionService : IPromptDefinitionService
{
    private readonly ILogger<PromptDefinitionService> _logger;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;

    public PromptDefinitionService(
        ILogger<PromptDefinitionService> logger,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task EnsurePromptDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring prompt definitions are up-to-date.");

        var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Use EF Core directly to fetch all prompt definitions
        var promptDefinitions = await dbContext.PromptDefinitions
        .Include(pd => pd.Variables)
        .ToListAsync(cancellationToken);

        var promptCatalogProperties = _defaultPromptCatalogTypes.GetType().GetProperties();
        var stringProperties = promptCatalogProperties.Where(p => p.PropertyType == typeof(string));

        int numberOfNewDefinitions = 0;

        foreach (var stringProperty in stringProperties)
        {
            var correspondingPromptDefinition = promptDefinitions.FirstOrDefault(pd => pd.ShortCode == stringProperty.Name);
            
            if (correspondingPromptDefinition != null)
            {
                continue;
            }

            var initialPromptContent = stringProperty.GetValue(_defaultPromptCatalogTypes)?.ToString() ?? "";
            var promptDefinition = new PromptDefinition
            {
                ShortCode = stringProperty.Name,
                Description = stringProperty.Name,
                Variables = []
            };

            if (!string.IsNullOrEmpty(initialPromptContent))
            {
                // Find words enclosed by double curly braces, distinct by variable name
                var variableMatches = Regex.Matches(initialPromptContent, @"\{\{ ?(\w+) ?\}\}")
                    .ToList()
                    .DistinctBy(match => match.Groups[1].Value);

                foreach (Match variableMatch in variableMatches)
                {
                    var variableName = variableMatch.Groups[1].Value;
                    var variable = new PromptVariableDefinition
                    {
                        VariableName = variableName,
                        PromptDefinitionId = promptDefinition.Id,
                        Description = "No description given"
                    };
                    promptDefinition.Variables.Add(variable);
                }
            }

            // Add the new prompt definition to the DbContext
            dbContext.PromptDefinitions.Add(promptDefinition);
            numberOfNewDefinitions++;
        }

        if (numberOfNewDefinitions > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created {Count} new prompt definitions.", numberOfNewDefinitions);
        }
        else
        {
            _logger.LogInformation("No missing prompt definitions found.");
        }
    }

}