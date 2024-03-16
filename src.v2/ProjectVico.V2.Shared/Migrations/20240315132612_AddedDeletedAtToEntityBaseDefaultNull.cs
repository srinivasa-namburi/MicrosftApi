using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedDeletedAtToEntityBaseDefaultNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Tables",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "TableCells",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "IngestedDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "GeneratedDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "DocumentMetadata",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ContentNodes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "BoundingRegions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "BoundingPolygons",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TableCells");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BoundingRegions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BoundingPolygons");
        }
    }
}
