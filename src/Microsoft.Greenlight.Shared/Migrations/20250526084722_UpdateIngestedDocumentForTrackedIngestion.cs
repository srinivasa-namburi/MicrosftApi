using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIngestedDocumentForTrackedIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IsDeleted",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "ClassificationShortCode",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "Plugin",
                table: "IngestedDocuments");

            migrationBuilder.RenameColumn(
                name: "OriginalDocumentUrl",
                table: "IngestedDocuments",
                newName: "FinalBlobUrl");

            migrationBuilder.AddColumn<int>(
                name: "DocumentLibraryType",
                table: "IngestedDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IngestionState",
                table: "IngestedDocuments",
                columns: new[] { "Container", "FolderPath", "FileName", "FileHash", "IngestionState" },
                unique: true,
                filter: "[FileHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IngestionState",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentLibraryType",
                table: "IngestedDocuments");

            migrationBuilder.RenameColumn(
                name: "FinalBlobUrl",
                table: "IngestedDocuments",
                newName: "OriginalDocumentUrl");

            migrationBuilder.AddColumn<string>(
                name: "ClassificationShortCode",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "IngestedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Plugin",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IsDeleted",
                table: "IngestedDocuments",
                columns: new[] { "Container", "FolderPath", "FileName", "FileHash", "IsDeleted" },
                unique: true,
                filter: "[FileHash] IS NOT NULL");
        }
    }
}
