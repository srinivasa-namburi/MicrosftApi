using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLastUpdatedUtcAndRenameProcessName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUpdatedUtc",
                table: "IngestedDocuments");

            migrationBuilder.RenameColumn(
                name: "DocumentProcess",
                table: "IngestedDocuments",
                newName: "DocumentLibraryOrProcessName");

            migrationBuilder.AddColumn<string>(
                name: "OriginalDocumentUrl",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalDocumentUrl",
                table: "IngestedDocuments");

            migrationBuilder.RenameColumn(
                name: "DocumentLibraryOrProcessName",
                table: "IngestedDocuments",
                newName: "DocumentProcess");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedUtc",
                table: "IngestedDocuments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
