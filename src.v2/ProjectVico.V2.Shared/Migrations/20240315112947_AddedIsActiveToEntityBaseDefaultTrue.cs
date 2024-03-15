using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedIsActiveToEntityBaseDefaultTrue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tables",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TableCells",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "IngestedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "GeneratedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DocumentMetadata",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ContentNodes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BoundingRegions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BoundingPolygons",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TableCells");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BoundingRegions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BoundingPolygons");
        }
    }
}
