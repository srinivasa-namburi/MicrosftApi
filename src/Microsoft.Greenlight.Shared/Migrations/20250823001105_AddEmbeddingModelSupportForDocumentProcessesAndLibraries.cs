using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingModelSupportForDocumentProcessesAndLibraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmbeddingModelDeploymentId",
                table: "DocumentLibraries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingSettings",
                table: "AiModels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelType",
                table: "AiModels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingSettings",
                table: "AiModelDeployments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions",
                column: "EmbeddingModelDeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraries_EmbeddingModelDeploymentId",
                table: "DocumentLibraries",
                column: "EmbeddingModelDeploymentId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLibraries_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DocumentLibraries");

            migrationBuilder.DropForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_AiModelDeployments_EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentLibraries_EmbeddingModelDeploymentId",
                table: "DocumentLibraries");

            migrationBuilder.DropColumn(
                name: "EmbeddingModelDeploymentId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "EmbeddingModelDeploymentId",
                table: "DocumentLibraries");

            migrationBuilder.DropColumn(
                name: "EmbeddingSettings",
                table: "AiModels");

            migrationBuilder.DropColumn(
                name: "ModelType",
                table: "AiModels");

            migrationBuilder.DropColumn(
                name: "EmbeddingSettings",
                table: "AiModelDeployments");
        }
    }
}
