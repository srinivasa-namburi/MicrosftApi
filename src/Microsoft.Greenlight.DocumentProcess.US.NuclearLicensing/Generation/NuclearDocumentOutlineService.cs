using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Models;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Generation;

public class NuclearDocumentOutlineService : IDocumentOutlineService
{
    private readonly ILogger<NuclearDocumentOutlineService> _logger;
    private readonly Kernel _sk;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public NuclearDocumentOutlineService(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("US.NuclearLicensing-Kernel")]
        Kernel sk,
        DocGenerationDbContext dbContext,
        ILogger<NuclearDocumentOutlineService> logger
        )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _sk = sk;
        _dbContext = dbContext;
        _logger = logger;

    }

    [Experimental("SKEXP0060")]
    public async Task<List<ContentNode>> GenerateDocumentOutlineForDocument(GeneratedDocument generatedDocument)
    {
        List<string> documentOutlineLines;

        var orderedSectionListExample = """
                                        1. Site Redress
                                        1.1 Description of Site Preparation Activities
                                        1.2 Site Redress Plan
                                        1.2.1 Site Redress Plan Objective and Considerations
                                        1.2.2 Description of Site Redress
                                        1.2.3 NRC Notification Upon Completion

                                        2. Need for Power

                                        3. Plant Description
                                        3.1 External Appearance and Plant Layout
                                        3.2 Reactor Power Conversion System
                                        3.2.1 Plant Water Use
                                        3.2.1.1 Water Consumption
                                        3.2.1.2 Water Treatment
                                        3.2.2 Cooling System
                                        3.2.2.1 Description and Operational Modes
                                        3.2.2.2 Component Descriptions

                                        4. Environmental Impacts of Construction
                                        4.1 Land-use impacts
                                        4.2 Water-related impacts
                                        4.3 Ecological impacts

                                        5. Socioeconomic Impacts
                                        5.1 Radiation Exposure to Construction Workers
                                        5.2 Measures and Controls to Limit Adverse Impacts During Construction

                                        6. Environmental Measurements and Monitoring Programs
                                        6.1 Summary of Monitoring Programs

                                        7. Environmental Impacts of Postulated Accidents Involving Radioactive Materials
                                        7.1 Conclusions

                                        8. Alternatives to the Proposed Action

                                        9. Alternative Sites

                                        10. Environmental Consequences of the Proposed Action
                                        """;

        if (_serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.UseFullDocumentOutlineGeneration)
        {
            var planner = new HandlebarsPlanner(new HandlebarsPlannerOptions()
            {
                AllowLoops = true
            });

            var request = "Please create a document outline for a nuclear environmental report";
            var plan = await planner.CreatePlanAsync(_sk, request);

            // Print the plan to log output
            _logger.LogInformation("Plan: \n {Plan}", plan);

            // Execute the plan
            var result = await plan.InvokeAsync(_sk);

            // Print the result to log output
            _logger.LogInformation("Result: {Result}", result);

            var cleanupRequest = $"""
                                  [SYSTEM]: Follow the instructions below accurately. Do not generate any introductory text or additional numbering,
                                  bullets, etc. Just the list of sections. \n\n
                                  [USER]:
                                  This is a list of chapters, sections and subsections in multiple levels. Please look for errors in the numbering of the sections and subsections.
                                  The numbering should be in the format [1.1.1] or [1.1] for a section and [1] for a chapter title.
                                  The chapter numbers should be consecutive numbers, so no skipping from 3 to 7 to 22 for example. The outer level should always be 1, 2, 3 etc.
                                  Please also remove any trailing spaces, quotes and dashes from the list.
                                  If the result has duplicate title or section numbers, please keep the order of the output intact, but renumber the titles and
                                  their children so that there are no duplicates. 
                                  Use proper capitalization - don't use ALL CAPS for the title or section names. Capitalize the first letter of each sentence.
                                  This is an example of the expected format:

                                  [FORMAT: {orderedSectionListExample}]\n

                                  The list of sections to process follows in the [CONTENT: <content>] section in this request.

                                  [CONTENT: {string.Join("\n", result)}]\n
                                  """;

            var cleanupResult = await _sk.InvokePromptAsync(cleanupRequest);
            documentOutlineLines = cleanupResult.GetValue<string>().Split("\n").ToList();
        }
        else
        {
            // This simply uses the example list of sections as the document outline without LLM Processing.
            documentOutlineLines = orderedSectionListExample.Split("\n").ToList();
        }

        var sectionDictionary = new Dictionary<string, string>();

        // Foreach line in the lines List, remove quotes as well as leading and trailing whitespace
        documentOutlineLines = documentOutlineLines.Select(x => x.Trim([' ', '"', '-'])
                .Replace("[", "")
                .Replace("]", ""))
            .ToList();

        // Remove any empty lines
        documentOutlineLines = documentOutlineLines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        // Create a dictionary that contains the section number (1, 1.1, 1.1.1, etc) as the key and the rest of the line as the value.
        foreach (var line in documentOutlineLines)
        {
            var sectionNumber = line.Split(' ')[0];
            var sectionTitle = line.Substring(sectionNumber.Length).Trim();
            sectionDictionary.Add(sectionNumber, sectionTitle);
        }

        // Remove any trailing periods from the section numbers
        sectionDictionary = sectionDictionary.ToDictionary(x => x.Key.TrimEnd('.'), x => x.Value);

        // Use the structure of the sections to determine a hierarchy - 1.1 is a child of 1, 1.1.1 is a child of 1.1, etc. Use this to create a tree of ContentNodes.
        // The ContentNodes will have a Text element that should contain the whole title - "1.1.1 Title" for example.
        // The Type should be Title for the top level, and Heading for the rest.
        // The Children should be a list of ContentNodes that are children of the current node.
        var contentNodeList = new List<ContentNode>();
        Dictionary<string, ContentNode> lastNodeAtLevel = new Dictionary<string, ContentNode>();

        foreach (var section in sectionDictionary)
        {
            var levels = section.Key.Split('.');
            var depth = levels.Length;
            var parentNodeKey = string.Join(".", levels.Take(depth - 1)); // Get parent node key by joining all but the last level

            ContentNode parentNode;
            if (depth == 1)
            {
                // This is a top-level node
                parentNode = null; // No parent
            }
            else if (!lastNodeAtLevel.TryGetValue(parentNodeKey, out parentNode))
            {
                // Parent node does not exist, which should not happen if input is correctly structured
                continue; // Or handle error
            }

            var currentNode = new ContentNode
            {
                Id = Guid.NewGuid(),
                Text = $"{section.Key} {section.Value}",
                Type = depth == 1 ? ContentNodeType.Title : ContentNodeType.Heading,
                GenerationState = ContentNodeGenerationState.OutlineOnly,
                Children = new List<ContentNode>()
            };

            if (parentNode != null)
            {
                parentNode.Children.Add(currentNode);
                currentNode.ParentId = parentNode.Id;
            }
            else
            {
                currentNode.ParentId = null;
                contentNodeList.Add(currentNode);
            }

            lastNodeAtLevel[section.Key] = currentNode; // Update the last node at this level
            _dbContext.ContentNodes.Add(currentNode);
        }

        await _dbContext.SaveChangesAsync();

        _dbContext.Attach(generatedDocument);
        generatedDocument.ContentNodes = contentNodeList;

        // Update the generated document in  the database
        await _dbContext.SaveChangesAsync();

        return contentNodeList;
    }
}
