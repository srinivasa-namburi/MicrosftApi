using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Helpers;
/// <summary>
/// Provides helper methods for rendering tables as HTML and replacing table references in documents.
/// </summary>
public class TableHelper
{
    private readonly DocGenerationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableHelper"/> class.
    /// </summary>
    /// <param name="dbContext">The database context to use for retrieving tables.</param>
    public TableHelper(DocGenerationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Renders the specified table as an HTML string.
    /// </summary>
    /// <param name="table">The table to render.</param>
    /// <returns>An HTML string representing the table.</returns>
    public static string RenderTableAsHtml(Table table)
    {
        var tableStringBuilder = new StringBuilder();
        tableStringBuilder.Append("<table>");

        // Initialize a 2D array to track occupied cells due to RowSpan.
        bool[,] occupied = new bool[table.RowCount, table.ColumnCount];

        // Sort cells by RowIndex and ColumnIndex to ensure correct rendering order.
        var sortedCells = table.Cells.OrderBy(cell => cell.RowIndex).ThenBy(cell => cell.ColumnIndex).ToList();

        for (int rowIndex = 0; rowIndex < table.RowCount; rowIndex++)
        {
            tableStringBuilder.Append("<tr>");

            for (int columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
            {
                if (occupied[rowIndex, columnIndex])
                {
                    // This position is occupied by a RowSpan from a previous row, skip rendering.
                    continue;
                }

                var cell = sortedCells.FirstOrDefault(c => c.RowIndex == rowIndex && c.ColumnIndex == columnIndex);
                if (cell != null)
                {
                    string cellTag = rowIndex == 0 ? "th" : "td";
                    string rowspanAttr = cell.RowSpan.HasValue ? $" rowspan='{cell.RowSpan}'" : "";
                    string colspanAttr = cell.ColumnSpan.HasValue ? $" colspan='{cell.ColumnSpan}'" : "";

                    tableStringBuilder.Append($"<{cellTag}{rowspanAttr}{colspanAttr}>{cell.Text}</{cellTag}>");

                    // Mark occupied positions due to RowSpan and ColumnSpan.
                    int rowSpan = cell.RowSpan ?? 1;
                    int columnSpan = cell.ColumnSpan ?? 1;
                    for (int i = 0; i < rowSpan; i++)
                    {
                        for (int j = 0; j < columnSpan; j++)
                        {
                            if (i == 0 && j < columnSpan) // Avoid overwriting the current cell's column span.
                                continue;
                            if (rowIndex + i < table.RowCount && columnIndex + j < table.ColumnCount)
                                occupied[rowIndex + i, columnIndex + j] = true;
                        }
                    }
                }
            }
            tableStringBuilder.Append("</tr>");
        }

        tableStringBuilder.Append("</table>");
        return tableStringBuilder.ToString();
    }

    /// <summary>
    /// Replaces table references in the document content with their HTML representations.
    /// </summary>
    /// <param name="documentContent">The document content containing table references.</param>
    /// <returns>The document content with table references replaced by HTML.</returns>
    public string ReplaceTableReferencesWithHtml(string documentContent)
    {
        // Table References look like this: "[TABLE_REFERENCE:4053b7b4-40b0-46b5-9cde-733f2b6ffb39]"
        // Look for all occurrences of the pattern and replace them with the corresponding table's HTML representation.
        // First, grab all the table IDs in the Content

        var tableIds = new List<Guid>();
        var matches = Regex.Matches(documentContent, @"\[TABLE_REFERENCE: (.*?)\]");

        if (matches.Count == 0)
        {
            return documentContent;
        }

        foreach (Match match in matches)
        {
            if (Guid.TryParse(match.Groups[1].Value, out var tableId))
            {
                tableIds.Add(tableId);
            }
        }

        // Get the tables from the database and use the RenderTableAsHtml method to get the HTML representation.

        var tables = _dbContext.Tables.Where(t => tableIds.Contains(t.Id))
            .Include(t => t.Cells)
            .AsSplitQuery()
            .ToList();

        foreach (var table in tables)
        {
            var tableHtml = RenderTableAsHtml(table);
            documentContent = documentContent.Replace($"[TABLE_REFERENCE: {table.Id}]", tableHtml);
        }

        return documentContent;
    }
}
