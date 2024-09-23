using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedUniquenessOfPromptVariableDefinitionVariableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromptVariableDefinitions_PromptDefinitionId",
                table: "PromptVariableDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PromptVariableDefinitions_VariableName",
                table: "PromptVariableDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_PromptDefinitionId_VariableName",
                table: "PromptVariableDefinitions",
                columns: new[] { "PromptDefinitionId", "VariableName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromptVariableDefinitions_PromptDefinitionId_VariableName",
                table: "PromptVariableDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_PromptDefinitionId",
                table: "PromptVariableDefinitions",
                column: "PromptDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_VariableName",
                table: "PromptVariableDefinitions",
                column: "VariableName",
                unique: true);
        }
    }
}
