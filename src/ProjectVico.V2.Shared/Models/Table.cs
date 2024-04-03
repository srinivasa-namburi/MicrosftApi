using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectVico.V2.Shared.Models ;
public class Table : EntityBase
{
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public ICollection<TableCell> Cells { get; set; }
    public ICollection<BoundingRegion> BoundingRegions { get; set; }

    public Guid? IngestedDocumentId { get; set; }
    [JsonIgnore]
    public virtual IngestedDocument? IngestedDocument { get; set; }

}