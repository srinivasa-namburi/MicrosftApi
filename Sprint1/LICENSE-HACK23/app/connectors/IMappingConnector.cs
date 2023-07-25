namespace app.connectors;

public interface IMappingConnector
{
    Task<List<string>> GetFacilitiesAsync(double latitude, double longitude, int radiusInMetres, int maxResults);

    Task<List<CategoryLandmarksResultSet>> GetLandmarksAsync(List<string> categorySearchStrings, double latitude, double longitude,
        int radiusInMetres, int maxResults);

    Task<List<string>> GetLandmarksAsync(string categorySearchString, double latitude, double longitude,
        int radiusInMetres, int maxResults);
}