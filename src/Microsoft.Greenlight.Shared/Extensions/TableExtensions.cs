
using Microsoft.Greenlight.Shared.Helpers;
using Table = Microsoft.Greenlight.Shared.Models.Table;
using TableCell = Microsoft.Greenlight.Shared.Models.TableCell;

namespace Microsoft.Greenlight.Shared.Extensions;

public static class TableExtensions
{
    
    public static IEnumerable<TableCell> GetCells(this Table table, int rowIndex, int columnIndex)
    {
        return table.Cells.Where(c => c.RowIndex == rowIndex && c.ColumnIndex == columnIndex);
    }

    public static IEnumerable<TableCell> GetCells(this Table table, int rowIndex, int columnIndex, int rowSpan,
        int columnSpan)
    {
        return table.Cells.Where(c => c.RowIndex == rowIndex && c.ColumnIndex == columnIndex && c.RowSpan == rowSpan && c.ColumnSpan == columnSpan);
    }
    
}
