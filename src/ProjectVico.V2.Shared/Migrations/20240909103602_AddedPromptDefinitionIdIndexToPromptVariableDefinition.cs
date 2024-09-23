using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedPromptDefinitionIdIndexToPromptVariableDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_PromptDefinitionId",
                table: "PromptVariableDefinitions",
                column: "PromptDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromptVariableDefinitions_PromptDefinitionId",
                table: "PromptVariableDefinitions");
        }
    }
}
