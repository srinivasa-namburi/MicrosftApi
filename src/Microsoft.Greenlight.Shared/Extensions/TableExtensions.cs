using Table = Microsoft.Greenlight.Shared.Models.Table;
using TableCell = Microsoft.Greenlight.Shared.Models.TableCell;

namespace Microsoft.Greenlight.Shared.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="Table"/> class.
/// </summary>
public static class TableExtensions
{
    /// <summary>
    /// Gets the cells from the specified table at the given row and column indices.
    /// </summary>
    /// <param name="table">The table to get cells from.</param>
    /// <param name="rowIndex">The row index of the cells to get.</param>
    /// <param name="columnIndex">The column index of the cells to get.</param>
    /// <returns>An enumerable of <see cref="TableCell"/> objects.</returns>
    public static IEnumerable<TableCell> GetCells(this Table table, int rowIndex, int columnIndex)
    {
        return table.Cells.Where(c => c.RowIndex == rowIndex && c.ColumnIndex == columnIndex);
    }

    /// <summary>
    /// Gets the cells from the specified table at the given row and column indices, with the specified row and column spans.
    /// </summary>
    /// <param name="table">The table to get cells from.</param>
    /// <param name="rowIndex">The row index of the cells to get.</param>
    /// <param name="columnIndex">The column index of the cells to get.</param>
    /// <param name="rowSpan">The row span of the cells to get.</param>
    /// <param name="columnSpan">The column span of the cells to get.</param>
    /// <returns>An enumerable of <see cref="TableCell"/> objects.</returns>
    public static IEnumerable<TableCell> GetCells(this Table table, int rowIndex, int columnIndex, int rowSpan, int columnSpan)
    {
        return table.Cells.Where(c => c.RowIndex == rowIndex && c.ColumnIndex == columnIndex && c.RowSpan == rowSpan && c.ColumnSpan == columnSpan);
    }
}
