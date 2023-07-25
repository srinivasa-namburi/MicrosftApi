//SK config

using System.Globalization;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text.Json;
using app.connectors;
using app.skills;
using Microsoft.SemanticKernel.Planning;

// Please make a copy of appsettings.json as a template and store your local changes in appsettings.development.json.
// This will allow you to store your local settings without checking them into source control.

var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
            .Build();

var embeddingConfig = configBuilder.GetRequiredSection("EmbeddingConfig").Get<Config>();
var completionConfig = configBuilder.GetRequiredSection("CompletionConfig").Get<Config>();

var sk = Kernel.Builder.Configure(embeddingConfig, completionConfig);
var context = sk.CreateNewContext();

//register skills
sk.CreateSemanticFunction(Assembly.GetEntryAssembly()!.LoadEmbeddedResource("app.skills.reverseclassifier.skprompt.txt"),
    functionName: "evaluate",
    skillName: "reverseclassifier",
    maxTokens: 2048);

//work out which parts of the application are important
var reverseClassifierSkill = sk.Skills.GetFunction("reverseclassifier", "evaluate");

//TODO: get this input from somewhere else, ie: read from the document instead
var input = @"2.2 SITE LOCATION AND LAYOUT 
2.2.1 Project Site Location 
The overall site (Figure 2.2-1) encompasses approximately 185 acres and is located within the East 
Tennessee Technology Park (ETTP) on the sites of the Former K-33 Facility and Former K-31 Facility. The 
ETTP, located in the northwest quadrant of the Oak Ridge Reservation (ORR), is adjacent to the Clinch 
River arm of Watts Bar Reservoir in Roane County and is approximately 13 miles west of downtown Oak 
Ridge, Tennessee (see Figure 2.2-2). The reactor would be located at approximately 35° 56’ 15.9” 
latitude, and -84° 24’ 11.2” longitude. 
Table 2.2-1 lists nearby federal facilities, industrial facilities, transportation, and residential facilities. 
Table 2.2-2 lists the sensitive populations (e.g., schools, daycare facilities, hospitals), nearest resident, 
and landmarks (including highways, transportation facilities, rivers, and other bodies of water) within 5 
miles of the site. No daycare centers or retirement homes are located within 5 miles of the site.";
context.Variables.Update(input);

context.Variables["documentDestination"] = "US Nuclear Regulatory Commission";
context.Variables["documentType"] = "Environmental Report";

var result = await reverseClassifierSkill.InvokeAsync(context);

if(result.ErrorOccurred)
{
    throw new Exception(result.LastErrorDescription);
}

var facets = JsonSerializer.Deserialize<IEnumerable<DocumentFacet>>(result.Result, new JsonSerializerOptions{ PropertyNameCaseInsensitive = true});

//set up connectors.
var mapsKey = completionConfig.AzureMapsKey;
var azureMapsConnector = new AzureMapsConnector(mapsKey);

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
sk.ImportSkill(new MapsSkill(azureMapsConnector), nameof(MapsSkill));

var customActionPlannerPrompt = Assembly.GetEntryAssembly()!.LoadEmbeddedResource("app.skills.actionplanner.skprompt.txt");

//build up a document as the model completes steps or the user enters data
var document = new Dictionary<string, string>();

foreach (var facet in facets)
{    
    var actionPlanner = new ActionPlanner(sk, customActionPlannerPrompt);    
    var plan = await actionPlanner.CreatePlanAsync($"Retrieve details using the appropriate skill, or by asking the user if a skill does not exist for the facet: {facet.Label}");
    
    //assume for now that we will always need the coordinates for each plan
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