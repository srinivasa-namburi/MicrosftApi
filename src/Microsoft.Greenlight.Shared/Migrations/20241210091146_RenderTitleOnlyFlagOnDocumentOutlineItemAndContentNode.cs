using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenderTitleOnlyFlagOnDocumentOutlineItemAndContentNode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RenderTitleOnly",
                table: "DocumentOutlineItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PromptInstructions",
                table: "ContentNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RenderTitleOnly",
                table: "ContentNodes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenderTitleOnly",
                table: "DocumentOutlineItems");

            migrationBuilder.DropColumn(
                name: "PromptInstructions",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "RenderTitleOnly",
                table: "ContentNodes");
        }
    }
}
