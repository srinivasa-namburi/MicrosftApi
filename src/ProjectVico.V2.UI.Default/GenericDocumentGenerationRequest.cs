namespace ProjectVico.V2.UI.US.NuclearLicensing;

public class GenericDocumentGenerationRequest
{
    public string DocumentProcessName {get;set; } = "US.NRC.EnvironmentalReport";
    
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocumentTitle { get; set; }
    public string? AuthorOid { get; set; }

    public string? StringifiedJson { get; set; }
}