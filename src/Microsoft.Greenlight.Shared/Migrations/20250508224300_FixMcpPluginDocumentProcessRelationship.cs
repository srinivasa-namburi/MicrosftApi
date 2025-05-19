using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class FixMcpPluginDocumentProcessRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_McpPluginDocumentProcesses_DynamicDocumentProcessDefinitions_DynamicDocumentProcessDefinitionId1",
                table: "McpPluginDocumentProcesses");

            migrationBuilder.DropIndex(
                name: "IX_McpPluginDocumentProcesses_DynamicDocumentProcessDefinitionId1",
                table: "McpPluginDocumentProcesses");

            migrationBuilder.DropColumn(
                name: "DynamicDocumentProcessDefinitionId1",
                table: "McpPluginDocumentProcesses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DynamicDocumentProcessDefinitionId1",
                table: "McpPluginDocumentProcesses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpPluginDocumentProcesses_DynamicDocumentProcessDefinitionId1",
                table: "McpPluginDocumentProcesses",
                column: "DynamicDocumentProcessDefinitionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_McpPluginDocumentProcesses_DynamicDocumentProcessDefinitions_DynamicDocumentProcessDefinitionId1",
                table: "McpPluginDocumentProcesses",
                column: "DynamicDocumentProcessDefinitionId1",
                principalTable: "DynamicDocumentProcessDefinitions",
                principalColumn: "Id");
        }
    }
}
