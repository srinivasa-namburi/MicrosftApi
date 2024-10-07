using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts;

namespace Microsoft.Greenlight.DocumentProcess.US.NuclearLicensing.Search;

public interface IUSNuclearLicensingRagRepository : IRagRepository
{
    Task<List<ReportDocument>> SearchOnlyTitlesAsync(string searchText, int top = 12, int k = 7);
    Task<List<ReportDocument>> SearchOnlySubsectionsAsync(string searchText, int top = 12, int k = 7);
    Task<IEnumerable<ReportDocument>> GetAllUniqueTitlesAsync(int numberOfUniqueFiles);
}
