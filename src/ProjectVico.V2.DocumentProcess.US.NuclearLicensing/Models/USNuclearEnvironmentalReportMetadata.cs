using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Models;

public class USNuclearEnvironmentalReportMetadata : MetadataDefinition
{
    protected override string MetadataType => "USNuclearEnvironmentalReportMetadata";
    public string? ReactorModel { get; set; }

    public LocationInformation? Location { get; set; } = new LocationInformation();
    public DateOnly? ProjectedProjectStartDate { get; set; }
    public DateOnly? ProjectedProjectEndDate { get; set; }

    // Plant Details
    public string? PlantName { get; set; }
    public IEnumerable<string> PlantDesign { get; set; } = new List<string>(); // set to a specific type later
    public string? OperatingHistory { get; set; }
    public string? AddressLine1 { get; set; } // there are also probably specific address types adaptable to country, etc.
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }

    // Proposed Changes
    public string? ModificationDescription { get; set; }
    public string? SafetyAnalysisReport { get; set; } // make a file later, likewise others would be other types
    public string? EnvironmentalImpact { get; set; }

    // Licensee Information
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? OrganizationalStructure { get; set; }
    public IEnumerable<string> FinancialAssurance { get; set; } = new List<string>();
    public string? ExperienceAndQualifications { get; set; }
 
}