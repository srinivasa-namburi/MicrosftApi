using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkSizeSettingsToDocumentLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VectorStoreChunkOverlap",
                table: "DocumentLibraries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VectorStoreChunkSize",
                table: "DocumentLibraries",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VectorStoreChunkOverlap",
                table: "DocumentLibraries");

            migrationBuilder.DropColumn(
                name: "VectorStoreChunkSize",
                table: "DocumentLibraries");
        }
    }
}
