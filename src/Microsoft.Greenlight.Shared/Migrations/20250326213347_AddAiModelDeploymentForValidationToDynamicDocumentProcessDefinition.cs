using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddAiModelDeploymentForValidationToDynamicDocumentProcessDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiModelDeploymentForValidationId",
                table: "DynamicDocumentProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_AiModelDeploymentForValidationId",
                table: "DynamicDocumentProcessDefinitions",
                column: "AiModelDeploymentForValidationId");

            migrationBuilder.AddForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_AiModelDeploymentForValidationId",
                table: "DynamicDocumentProcessDefinitions",
                column: "AiModelDeploymentForValidationId",
                principalTable: "AiModelDeployments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_AiModelDeploymentForValidationId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_AiModelDeploymentForValidationId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "AiModelDeploymentForValidationId",
                table: "DynamicDocumentProcessDefinitions");
        }
    }
}
