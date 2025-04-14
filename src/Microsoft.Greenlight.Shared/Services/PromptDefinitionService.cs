using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Repositories;
using System.Text.RegularExpressions;

namespace Microsoft.Greenlight.Shared.Services;

/// <inheritdoc />
public class PromptDefinitionService : IPromptDefinitionService
{
    private readonly ILogger<PromptDefinitionService> _logger;
    private readonly PromptDefinitionRepository _promptDefinitionRepository;
    private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;

    public PromptDefinitionService(
        ILogger<PromptDefinitionService> logger,
        PromptDefinitionRepository promptDefinitionRepository)
    {
        _logger = logger;
        _promptDefinitionRepository = promptDefinitionRepository;
        _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
    }

    /// <inheritdoc />
    public async Task EnsurePromptDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring prompt definitions are up-to-date.");

        var promptCatalogProperties = _defaultPromptCatalogTypes.GetType().GetProperties();
        var stringProperties = promptCatalogProperties.Where(p => p.PropertyType == typeof(string));

        var promptDefinitions = await _promptDefinitionRepository.GetAllPromptDefinitionsAsync(true);

        int numberOfNewDefinitions = 0;
        foreach (var stringProperty in stringProperties)
        {
            var correspondingPromptDefinition = promptDefinitions.FirstOrDefault(pd => pd.ShortCode == stringProperty.Name);
            if (correspondingPromptDefinition == null)
            {
                var initialPromptContent = stringProperty.GetValue(_defaultPromptCatalogTypes)?.ToString() ?? "";
                var promptDefinition = new PromptDefinition
                {
                    ShortCode = stringProperty.Name,
                    Description = stringProperty.Name,
                };

                if (!string.IsNullOrEmpty(initialPromptContent))
                {
                    // Find words enclosed by double curly braces, distinct by variable name.
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

                await _promptDefinitionRepository.AddAsync(promptDefinition, false);
                numberOfNewDefinitions++;
            }
        }

        if (numberOfNewDefinitions > 0)
        {
            await _promptDefinitionRepository.SaveChangesAsync();
            _logger.LogInformation("Created {Count} new prompt definitions.", numberOfNewDefinitions);
        }
        else
        {
            _logger.LogInformation("No missing prompt definitions found.");
        }
    }
}