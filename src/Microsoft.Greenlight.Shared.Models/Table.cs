using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models;
/// <summary>
/// Represents a table entity with rows, columns, cells, and bounding regions.
/// </summary>
public class Table : EntityBase
{
    /// <summary>
    /// Number of rows in the table.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Number of columns in the table.
    /// </summary>
    public int ColumnCount { get; set; }

    /// <summary>
    /// Collection of cells in the table.
    /// </summary>
    public ICollection<TableCell>? Cells { get; set; }

    /// <summary>
    /// Collection of bounding regions in the table.
    /// </summary>
    public ICollection<BoundingRegion>? BoundingRegions { get; set; }

    /// <summary>
    /// Unique ID of the ingested document associated with the table.
    /// </summary>
    public Guid? IngestedDocumentId { get; set; }

    /// <summary>
    /// Ingested document associated with the table.
    /// </summary>
    [JsonIgnore]
    public virtual IngestedDocument? IngestedDocument { get; set; }
}
