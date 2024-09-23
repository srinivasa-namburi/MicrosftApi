using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDocumentLinkWithType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn
                (
                    name: "Type",
                    table: "ExportedDocumentLinks"
                );

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ExportedDocumentLinks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "ExportedDocumentLinks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "ExportedDocumentLinks");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ExportedDocumentLinks"
            );

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ExportedDocumentLinks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "application/octet-stream");

        }
    }
}
