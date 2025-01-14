using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a cell in a table with properties for row and column indices,
/// spans, text content, and associated table.
/// </summary>
public class TableCell : EntityBase
{
    /// <summary>
    /// Row index of the cell.
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Column index of the cell.
    /// </summary>
    public int ColumnIndex { get; set; }

    /// <summary>
    /// Row span of the cell.
    /// </summary>
    public int? RowSpan { get; set; }

    /// <summary>
    /// Column span of the cell.
    /// </summary>
    public int? ColumnSpan { get; set; }

    /// <summary>
    /// Text content of the cell.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Unique identifier of the associated table.
    /// </summary>
    public Guid TableId { get; set; }

    /// <summary>
    /// Table associated with the cell.
    /// </summary>
    [JsonIgnore]
    public virtual Table? Table { get; set; }
}
