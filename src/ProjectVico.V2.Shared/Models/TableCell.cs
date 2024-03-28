using System.Text.Json.Serialization;

namespace ProjectVico.V2.Shared.Models;

public class TableCell : EntityBase
{

    public int RowIndex { get; set; }
    public int ColumnIndex { get; set; }
    public int? RowSpan { get; set; }
    public int? ColumnSpan { get; set; }
    public string Text { get; set; }
    public Guid TableId { get; set; }
    [JsonIgnore]
    public virtual Table Table { get; set; }
}