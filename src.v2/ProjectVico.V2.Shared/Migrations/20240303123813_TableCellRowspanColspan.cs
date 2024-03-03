using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class TableCellRowspanColspan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ColumnSpan",
                table: "TableCells",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowSpan",
                table: "TableCells",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColumnSpan",
                table: "TableCells");

            migrationBuilder.DropColumn(
                name: "RowSpan",
                table: "TableCells");
        }
    }
}
