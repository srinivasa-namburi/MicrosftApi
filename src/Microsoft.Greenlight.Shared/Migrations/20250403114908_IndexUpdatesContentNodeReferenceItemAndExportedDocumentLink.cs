using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class IndexUpdatesContentNodeReferenceItemAndExportedDocumentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FileHash",
                table: "ExportedDocumentLinks",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDocumentLinks_FileHash",
                table: "ExportedDocumentLinks",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDocumentLinks_Id",
                table: "ExportedDocumentLinks",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceItems_ContentReferenceSourceId",
                table: "ContentReferenceItems",
                column: "ContentReferenceSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceItems_Id",
                table: "ContentReferenceItems",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceItems_Id_ReferenceType",
                table: "ContentReferenceItems",
                columns: new[] { "Id", "ReferenceType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExportedDocumentLinks_FileHash",
                table: "ExportedDocumentLinks");

            migrationBuilder.DropIndex(
                name: "IX_ExportedDocumentLinks_Id",
                table: "ExportedDocumentLinks");

            migrationBuilder.DropIndex(
                name: "IX_ContentReferenceItems_ContentReferenceSourceId",
                table: "ContentReferenceItems");

            migrationBuilder.DropIndex(
                name: "IX_ContentReferenceItems_Id",
                table: "ContentReferenceItems");

            migrationBuilder.DropIndex(
                name: "IX_ContentReferenceItems_Id_ReferenceType",
                table: "ContentReferenceItems");

            migrationBuilder.AlterColumn<string>(
                name: "FileHash",
                table: "ExportedDocumentLinks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
