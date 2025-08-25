using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class EmbeddingSettingsAdditional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLibraries_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DocumentLibraries");

            migrationBuilder.DropForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLibraries_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DocumentLibraries",
                column: "EmbeddingModelDeploymentId",
                principalTable: "AiModelDeployments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions",
                column: "EmbeddingModelDeploymentId",
                principalTable: "AiModelDeployments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLibraries_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DocumentLibraries");

            migrationBuilder.DropForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLibraries_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DocumentLibraries",
                column: "EmbeddingModelDeploymentId",
                principalTable: "AiModelDeployments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions",
                column: "EmbeddingModelDeploymentId",
                principalTable: "AiModelDeployments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
