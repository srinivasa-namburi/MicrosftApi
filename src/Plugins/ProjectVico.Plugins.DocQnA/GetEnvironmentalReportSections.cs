using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectVico.Plugins.DocQnA.Options;


namespace ProjectVico.Plugins.DocQnA;

public class GetEnvironmentalReportSections
{
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly ILogger _logger;

    public GetEnvironmentalReportSections(ILoggerFactory loggerFactory, IOptions<AiOptions> aiOptions)
    {
        this._aiOptions = aiOptions;
        //_logger = loggerFactory.CreateLogger<DemoHttpTrigger>();
    }

    [Function("GetEnvironmentalReportSections")]
    [OpenApiOperation(operationId: "GetEnvironmentalReportSections", tags: new[] { "ExecuteFunction" }, Description = "Gets a list of sections for an environmental report section.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns a full list of all sections required to complete an environmental report as plain text")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");
      
        // Read the sections from disk (data\EnvironmentalReportSections.txt), and add them to a StringBuilder
        var sections = new StringBuilder();
        var lines = await File.ReadAllLinesAsync("data\\EnvironmentalReportSections.txt");
        foreach (var line in lines)
        {
            sections.AppendLine(line);
        }
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString(sections.ToString());

        return response;
    }
}
