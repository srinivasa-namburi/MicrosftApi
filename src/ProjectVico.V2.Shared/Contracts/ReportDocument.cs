namespace ProjectVico.V2.Shared.Contracts;

public class ReportDocument
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string? ParentId { get; set; }
    public string? ParentTitle { get; set; }
    public string OriginalFileName { get; set; }
    public string OriginalFileHash { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[] TitleVector { get; set; }
    public float[] ContentVector { get; set; }

}