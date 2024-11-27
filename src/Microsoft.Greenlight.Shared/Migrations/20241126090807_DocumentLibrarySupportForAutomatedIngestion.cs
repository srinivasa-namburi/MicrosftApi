using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentLibrarySupportForAutomatedIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DocumentProcessName",
                table: "DocumentIngestionSagaStates",
                newName: "DocumentLibraryShortName");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentIngestionSagaStates_DocumentProcessName",
                table: "DocumentIngestionSagaStates",
                newName: "IX_DocumentIngestionSagaStates_DocumentLibraryShortName");

            migrationBuilder.AddColumn<int>(
                name: "DocumentLibraryType",
                table: "DocumentIngestionSagaStates",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentLibraryType",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.RenameColumn(
                name: "DocumentLibraryShortName",
                table: "DocumentIngestionSagaStates",
                newName: "DocumentProcessName");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentIngestionSagaStates_DocumentLibraryShortName",
                table: "DocumentIngestionSagaStates",
                newName: "IX_DocumentIngestionSagaStates_DocumentProcessName");
        }
    }
}
