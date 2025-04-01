using System.Text.RegularExpressions;
using Microsoft.Greenlight.DocumentProcess.Shared.Prompts;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Repositories;
using Quartz;

namespace Microsoft.Greenlight.Worker.Scheduler.Jobs
{
    /// <summary>
    /// Quartz job that creates missing generic prompt definitions.
    /// </summary>
    public class CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob : IJob
    {
        private readonly PromptDefinitionRepository _promptDefinitionRepository;
        private readonly ILogger<CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob> _logger;
        private readonly DefaultPromptCatalogTypes _defaultPromptCatalogTypes;

        public CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob(
            PromptDefinitionRepository promptDefinitionRepository,
            ILogger<CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob> logger)
        {
            _promptDefinitionRepository = promptDefinitionRepository;
            _logger = logger;
            _defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob started at {time}", DateTimeOffset.Now);

            try
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

                        if (!string.IsNullOrEmpty(initialPromptContent))
                        {
                            // Find words enclosed by double curly braces, distinct by variable name.
                            var variableMatches = Regex.Matches(initialPromptContent, @"\{\{ ?(\w+) ?\}\>")
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
                    _logger.LogInformation("CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob: Created {Count} new prompt definitions.", numberOfNewDefinitions);
                }
                else
                {
                    _logger.LogInformation("CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob: No missing prompt definitions found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob execution");
                throw; // rethrow if you want Quartz to record a job failure
            }
        }
    }
}
