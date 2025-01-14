using System.Text.RegularExpressions;
using Microsoft.Greenlight.DocumentProcess.Shared.Prompts;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;

namespace Microsoft.Greenlight.Worker.Scheduler;

/// <summary>
/// Worker service to maintain dynamic document process by creating missing generic prompt definitions.
/// </summary>
public class DynamicDocumentProcessMaintenanceWorker : BackgroundService
{
    private readonly PromptDefinitionRepository _promptDefinitionRepository;
    private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicDocumentProcessMaintenanceWorker"/> class.
    /// </summary>
    /// <param name="promptDefinitionRepository">The repository for prompt definitions.</param>
    public DynamicDocumentProcessMaintenanceWorker(PromptDefinitionRepository promptDefinitionRepository)
    {
        _promptDefinitionRepository = promptDefinitionRepository;
        _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
    }

    /// <summary>
    /// Executes the worker service.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the background service operation.</returns>    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Find all Prompt Catalog Properties of type String that don't have a corresponding Prompt Definition
            await CreateMissingGenericPromptDefinitions();

            // Repeat every 1 hour
            await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
        }
    }

    /// <summary>
    /// Creates missing generic prompt definitions.
    /// </summary>
    /// <returns>A task that represents the </returns>
    private async Task CreateMissingGenericPromptDefinitions()
    {
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

                // Extract variable information from the generic prompt definition
                if (!string.IsNullOrEmpty(initialPromptContent))
                {
                    // Find words enclosed by double curly braces with or without spaces
                    // after the one or two opening curly braces and before the one or two closing curly braces - these are the variables
                    // Find only distinct variables, remove duplicates

                    var variableMatches = Regex.Matches(initialPromptContent, @"\{\{ ?(\w+) ?\}\}").ToList().DistinctBy(x => x.Groups[1].Value);

                    foreach (Match variableMatch in variableMatches)
                    {
                        var variableName = variableMatch.Groups[1].Value;
                        var variable = new PromptVariableDefinition()
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
        }
    }
}
