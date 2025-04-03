using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class FileHashForContentNodeReferenceItemAndExportedDocumentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ExportedDocumentLinks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ContentReferenceItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ExportedDocumentLinks");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ContentReferenceItems");
        }
    }
}
