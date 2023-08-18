//SK config

using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using LicenseCopilot.Framework;
using LicenseCopilot.Framework.Connectors;

// Please make a copy of appsettings.json as a template and store your local changes in appsettings.development.json.
// This will allow you to store your local settings without checking them into source control.

var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
            .Build();

var embeddingConfig = configBuilder.GetRequiredSection("EmbeddingConfig").Get<Config>();
var completionConfig = configBuilder.GetRequiredSection("CompletionConfig").Get<Config>();
var mapsKey = configBuilder.GetValue<string>("AzureMapsKey");

var sk = Kernel.Builder.Configure(embeddingConfig, completionConfig);

var environmentalReportUri = "https://www.nrc.gov/docs/ML2130/ML21306A133.pdf";
var pageNumber = 32;

//get a temporary file name on disk of type pdf
var tempFileName = Path.GetTempFileName() + ".pdf";

using (var httpClient = new HttpClient())
{
    var reportStream = await httpClient.GetStreamAsync(environmentalReportUri);
    
    using (var fileStream = File.Create(tempFileName))
    {
        await reportStream.CopyToAsync(fileStream);
    }
}

var processor = new PdfProcessor(sk);
var section = await processor.LoadPage(tempFileName, pageNumber);

var documentCopilot = new DocumentCopilot(sk, new AzureMapsConnector(mapsKey));
await documentCopilot.EvaluateSectionAsync(section);
