
using ProjectVico.V2.Shared.Helpers;
using Table = ProjectVico.V2.Shared.Models.Table;
using TableCell = ProjectVico.V2.Shared.Models.TableCell;

namespace ProjectVico.V2.Shared.Extensions;

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