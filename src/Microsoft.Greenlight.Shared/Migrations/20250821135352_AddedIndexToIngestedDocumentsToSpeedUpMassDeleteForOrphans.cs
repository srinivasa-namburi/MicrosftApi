using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedIndexToIngestedDocumentsToSpeedUpMassDeleteForOrphans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure existing values fit into nvarchar(450) to avoid truncation errors on ALTER COLUMN
            migrationBuilder.Sql(@"
UPDATE IngestedDocuments
SET DocumentLibraryOrProcessName = LEFT(DocumentLibraryOrProcessName, 450)
WHERE DocumentLibraryOrProcessName IS NOT NULL AND LEN(DocumentLibraryOrProcessName) > 450;
");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentLibraryOrProcessName",
                table: "IngestedDocuments",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_DocumentLibraryType_DocumentLibraryOrProcessName",
                table: "IngestedDocuments",
                columns: new[] { "DocumentLibraryType", "DocumentLibraryOrProcessName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_DocumentLibraryType_DocumentLibraryOrProcessName",
                table: "IngestedDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentLibraryOrProcessName",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
