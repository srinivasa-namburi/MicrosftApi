using System.Reflection;
using System.Text.Json;
using LicenseCopilot.Framework.Connectors;
using LicenseCopilot.Framework.Model;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;

namespace LicenseCopilot.Framework;

public class DocumentCopilot
{
    private readonly IKernel _kernel;
    private readonly ISKFunction _reverseClassifierSkill;
    private readonly IMappingConnector _mappingConnector;

    public DocumentCopilot(IKernel sk, IMappingConnector mappingConnector)
    {
        this._kernel = sk;
        this._mappingConnector = mappingConnector;

        sk.CreateSemanticFunction(Assembly.GetExecutingAssembly()!.LoadEmbeddedResource("LicenseCopilot.Framework.skills.reverseclassifier.skprompt.txt"),
            functionName: "evaluate",
            skillName: "reverseclassifier",
            maxTokens: 2048);
            
        //work out which parts of the application are important
        this._reverseClassifierSkill = sk.Skills.GetFunction("reverseclassifier", "evaluate");
    }

    public async Task<IEnumerable<DocumentFacet>> FindFacetsAsync(DocumentSection section)
    {
        var context = this._kernel.CreateNewContext();

        context.Variables.Update(section.Text);

        context.Variables["documentDestination"] = "US Nuclear Regulatory Commission";
        context.Variables["documentType"] = "Environmental Report";

        var result = await this._reverseClassifierSkill.InvokeAsync(context);

        if (result.ErrorOccurred)
        {
            throw new Exception(result.LastErrorDescription);
        }

        return JsonSerializer.Deserialize<IEnumerable<DocumentFacet>>(result.Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task EvaluateSectionAsync(DocumentSection section)
    {
        var facets = await this.FindFacetsAsync(section);

        Console.WriteLine("Please enter the latitude and longitude of the site (eg: 123,456): ");

        var latLong = Console.ReadLine();

        if(latLong == null)
        {
            Console.WriteLine("Must enter latitude and longitude. Exiting.");
            return;
        }

        var latitude = latLong.Split(',')[0];
        var longitude = latLong.Split(',')[1];

        //import any skills here that we want planner to be able to select from
        this._kernel.ImportSkill(new MapsSkill(this._mappingConnector), nameof(MapsSkill));

        var customActionPlannerPrompt = Assembly.GetExecutingAssembly()!.LoadEmbeddedResource("LicenseCopilot.Framework.skills.actionplanner.skprompt.txt");

        //build up a document as the model completes steps or the user enters data
        var document = new Dictionary<string, string>();

        foreach (var facet in facets)
        {    
            var actionPlanner = new ActionPlanner(this._kernel, customActionPlannerPrompt);    
            var plan = await actionPlanner.CreatePlanAsync($"Retrieve details using the appropriate skill, " +
                $"or by asking the user if a skill does not exist for the facet: {facet.Label}");
            
            //assume for now that we will always need the coordinates for each plan
            //TODO: try bumping these up to the plan ask
            plan.State["latitude"] = latitude;
            plan.State["longitude"] = longitude;
            plan.State["radius"] = "50000"; //default to 50km

            var planResult = await plan.InvokeAsync();

            if (planResult.Result.Length == 0)
            {
                //model didn't find an answer, just ask the user
                Console.WriteLine($"Facet: [{facet.Label}] requires input.");
                Console.WriteLine($"> {facet.Reason}");
                Console.WriteLine("Your input:");

                var details = Console.ReadLine();
                
                document[facet.Label] = details;
            }
            else
            {
                //Console.WriteLine("==");
                Console.WriteLine($"Facet: [{facet.Label}] was resolved by the model");
                document[facet.Label] = planResult.Result;
                //Console.WriteLine("Plan result: " + planResult.Result);
                //Console.WriteLine("==");
                //Console.WriteLine();
            }    
        }

        //output the document state
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Section ready for review");
        Console.WriteLine();

        foreach (var item in document)
        {
            Console.WriteLine(item.Key + ": " + item.Value);
        }

        Console.WriteLine();

        //then everything else based on the important parts of the application as dictated by the model

        //where we have skills that can fulfill the need, we should first try to resolve the text via the skills (this can be via ActionPlanner or similar)
        //otherwise we need to ask the user to enter those details for us
        //it's assumed that post-generation, all aspects of the document would be manually reviewed by a human
        //AND it may also be interesting to ask the AI to review the document as a whole vs the base application as it may spot areas for refinement also
                
    }
}