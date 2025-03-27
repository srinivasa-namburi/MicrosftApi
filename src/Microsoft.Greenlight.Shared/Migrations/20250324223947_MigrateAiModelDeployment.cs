using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAiModelDeployment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiCompletionModelDeploymentName",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "AiValidationModelDeploymentName",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.AddColumn<Guid>(
                name: "AiModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_AiModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions",
                column: "AiModelDeploymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_AiModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions",
                column: "AiModelDeploymentId",
                principalTable: "AiModelDeployments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_AiModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_AiModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "AiModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

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
    }
}
