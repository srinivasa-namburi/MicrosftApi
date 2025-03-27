using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedAiModelParametersForDocumentProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiCompletionModelDeploymentName",
                table: "DynamicDocumentProcessDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiValidationModelDeploymentName",
                table: "DynamicDocumentProcessDefinitions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiCompletionModelDeploymentName",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "AiValidationModelDeploymentName",
                table: "DynamicDocumentProcessDefinitions");
        }
    }
}
