using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Contracts;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Search;

public interface IUSNuclearLicensingRagRepository : IRagRepository
{
    Task<List<ReportDocument>> SearchOnlyTitlesAsync(string searchText, int top = 12, int k = 7);
    Task<List<ReportDocument>> SearchOnlySubsectionsAsync(string searchText, int top = 12, int k = 7);
    Task<IEnumerable<ReportDocument>> GetAllUniqueTitlesAsync(int numberOfUniqueFiles);
}