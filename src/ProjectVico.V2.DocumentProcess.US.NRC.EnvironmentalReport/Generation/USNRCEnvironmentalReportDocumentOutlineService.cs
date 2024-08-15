using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.US.NRC.EnvironmentalReport.Generation;

public class USNRCEnvironmentalReportDocumentOutlineService : IDocumentOutlineService
{
    private readonly ILogger<USNRCEnvironmentalReportDocumentOutlineService> _logger;
    private readonly Kernel _sk;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public USNRCEnvironmentalReportDocumentOutlineService(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("US.NRC.EnvironmentalReport-Kernel")]
        Kernel sk,
        DocGenerationDbContext dbContext,
        ILogger<USNRCEnvironmentalReportDocumentOutlineService> logger
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

        // Original sample. 
        #region Original sample - commented out
        //var orderedSectionListExample = """
        //                                1. Site Redress
        //                                1.1 Description of Site Preparation Activities
        //                                1.2 Site Redress Plan
        //                                1.2.1 Site Redress Plan Objective and Considerations
        //                                1.2.2 Description of Site Redress
        //                                1.2.3 NRC Notification Upon Completion

        //                                2. Need for Power

        //                                3. Plant Description
        //                                3.1 External Appearance and Plant Layout
        //                                3.2 Reactor Power Conversion System
        //                                3.2.1 Plant Water Use
        //                                3.2.1.1 Water Consumption
        //                                3.2.1.2 Water Treatment
        //                                3.2.2 Cooling System
        //                                3.2.2.1 Description and Operational Modes
        //                                3.2.2.2 Component Descriptions

        //                                4. Environmental Impacts of Construction
        //                                4.1 Land-use impacts
        //                                4.2 Water-related impacts
        //                                4.3 Ecological impacts

        //                                5. Socioeconomic Impacts
        //                                5.1 Radiation Exposure to Construction Workers
        //                                5.2 Measures and Controls to Limit Adverse Impacts During Construction

        //                                6. Environmental Measurements and Monitoring Programs
        //                                6.1 Summary of Monitoring Programs

        //                                7. Environmental Impacts of Postulated Accidents Involving Radioactive Materials
        //                                7.1 Conclusions

        //                                8. Alternatives to the Proposed Action

        //                                9. Alternative Sites

        //                                10. Environmental Consequences of the Proposed Action
        //                                """;
        #endregion

        var orderedSectionListExample = """
                                        1. Introduction
                                        1.1. Project Overview
                                        1.2. Applicant Information
                                        1.3. Site Location
                                        1.4. Regulatory Requirements
                                        2. Environmental Description
                                        2.1. Land Use and Geology
                                        2.1.1. Topography
                                        2.1.2. Soil Characteristics
                                        2.1.3. Seismic Conditions
                                        2.2. Water Resources
                                        2.2.1. Surface Water
                                        2.2.2. Groundwater
                                        2.3. Ecology
                                        2.3.1. Terrestrial Ecology
                                        2.3.2. Aquatic Ecology
                                        2.4. Climate and Meteorology
                                        2.4.1. Local Climate
                                        2.4.2. Meteorological Data
                                        3. Plant Description
                                        3.1. Plant Layout
                                        3.2. Reactor Design
                                        3.2.1. Reactor Core
                                        3.2.2. Safety Systems
                                        3.3. Auxiliary Systems
                                        3.3.1. Cooling Systems
                                        3.3.2. Waste Management Systems
                                        4. Environmental Impacts of Construction
                                        4.1. Land Disturbance
                                        4.2. Air Quality
                                        4.2.1. Dust Generation
                                        4.2.2. Emissions from Equipment
                                        4.3. Water Quality
                                        4.4. Noise Levels
                                        4.5. Waste Generation
                                        4.5.1. Solid Waste
                                        4.5.2. Hazardous Waste
                                        5. Environmental Impacts of Station Operation
                                        5.1. Air Quality
                                        5.1.1. Routine Emissions
                                        5.1.2. Accidental Releases
                                        5.2. Water Quality
                                        5.2.1. Thermal Discharge
                                        5.2.2. Chemical Discharge
                                        5.3. Land Use
                                        5.4. Ecology
                                        5.4.1. Terrestrial Impacts
                                        5.4.2. Aquatic Impacts
                                        6. Environmental Measurement and Monitoring Programs
                                        6.1. Air Monitoring
                                        6.2. Water Monitoring
                                        6.3. Ecological Monitoring
                                        6.4. Program Management
                                        6.4.1. Data Collection
                                        6.4.2. Data Analysis
                                        7. Environmental Impacts of Postulated Accidents Involving Radioactive Materials
                                        7.1. Accident Scenarios
                                        7.2. Radiological Consequences
                                        7.3. Mitigation Measures
                                        7.4. Health Risks
                                        8. Need for Power
                                        8.1. Regional Energy Demand
                                        8.2. Alternative Energy Sources
                                        8.3. Future Projections
                                        8.4. Justification for the Proposed Project
                                        9. Alternatives to the Proposed Action
                                        9.1. No-Action Alternative
                                        9.2. Alternative Sites
                                        9.3. Alternative Technologies
                                        9.4. Comparison of Alternatives
                                        10. Non-Radiological Health Impacts
                                        10.1. Occupational Health
                                        10.2. Public Health
                                        10.3. Noise
                                        10.4. Air Emissions
                                        11. Radiological Health Impacts
                                        11.1. Worker Exposure
                                        11.2. Public Exposure
                                        11.3. Dose Assessment
                                        11.4. Radiation Protection
                                        12. Cumulative Impacts
                                        12.1. Past Actions
                                        12.2. Present Actions
                                        12.3. Future Actions
                                        12.4. Combined Impacts
                                        13. Mitigation Measures
                                        13.1. Design Features
                                        13.2. Operational Controls
                                        13.3. Monitoring Programs
                                        13.4. Contingency Plans
                                        14. Conclusions
                                        14.1. Summary of Findings
                                        14.2. Environmental Significance
                                        14.3. Effectiveness of Mitigation
                                        14.4. Recommendations
                                        15. References
                                        15.1. Scientific Studies
                                        15.2. Regulatory Documents
                                        15.3. Technical Reports
                                        15.4. Other Sources
                                        16. Appendices
                                        16.1. Technical Data
                                        16.2. Modeling Results
                                        16.3. Supporting Documentation
                                        16.4. Additional Information
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
