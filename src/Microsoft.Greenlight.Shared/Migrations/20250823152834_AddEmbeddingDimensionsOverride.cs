using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingDimensionsOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmbeddingDimensionsOverride",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingDimensionsOverride",
                table: "DocumentLibraries",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingDimensionsOverride",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "EmbeddingDimensionsOverride",
                table: "DocumentLibraries");
        }
    }
}
