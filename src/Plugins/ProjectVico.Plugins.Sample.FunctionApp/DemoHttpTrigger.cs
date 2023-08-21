using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace ProjectVico.Plugins.Sample.FunctionApp;

public class DemoHttpTrigger
{
    private readonly ILogger _logger;

    public DemoHttpTrigger(ILoggerFactory loggerFactory)
    {
        //_logger = loggerFactory.CreateLogger<DemoHttpTrigger>();
    }

    [Function("DemoHttpTrigger")]
    [OpenApiOperation(operationId: "Query", tags: new[] { "ExecuteFunction" }, Description = "Returns the same string back as passed in")]
    [OpenApiParameter(name: "number1", Description = "The first number to add", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "number2", Description = "The second number to add", Required = true, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Returns the sum of the two numbers.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        //_logger.LogInformation("C# HTTP trigger function processed a request.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString(req.Body.ToString() ?? string.Empty);

        return response;
    }
}
