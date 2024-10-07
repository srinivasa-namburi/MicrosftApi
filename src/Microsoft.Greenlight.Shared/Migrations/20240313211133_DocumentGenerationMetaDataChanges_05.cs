using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentGenerationMetaDataChanges_05 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentMetadata_GeneratedDocuments_GeneratedDocumentId",
                table: "DocumentMetadata");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentMetadata_GeneratedDocuments_GeneratedDocumentId",
                table: "DocumentMetadata",
                column: "GeneratedDocumentId",
                principalTable: "GeneratedDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentMetadata_GeneratedDocuments_GeneratedDocumentId",
                table: "DocumentMetadata");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentMetadata_GeneratedDocuments_GeneratedDocumentId",
                table: "DocumentMetadata",
                column: "GeneratedDocumentId",
                principalTable: "GeneratedDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
