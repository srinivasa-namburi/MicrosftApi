using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class SetCascadingDeletesForPromptImplementations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentOutlines_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                table: "DocumentOutlines");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentOutlines_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                table: "DocumentOutlines",
                column: "DocumentProcessDefinitionId",
                principalTable: "DynamicDocumentProcessDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentOutlines_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                table: "DocumentOutlines");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentOutlines_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                table: "DocumentOutlines",
                column: "DocumentProcessDefinitionId",
                principalTable: "DynamicDocumentProcessDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
