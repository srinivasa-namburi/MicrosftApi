using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DynamicPluginRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DynamicPluginDocumentProcesses");

            migrationBuilder.DropTable(
                name: "DynamicPlugins");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessMetaDataFields_DynamicDocumentProcessDefinitionId_Name",
                table: "DynamicDocumentProcessMetaDataFields");

            migrationBuilder.AddColumn<Guid>(
                name: "DynamicDocumentProcessDefinitionId1",
                table: "McpPluginDocumentProcesses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DynamicDocumentProcessMetaDataFields",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DynamicDocumentProcessMetaDataFields",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "DynamicPlugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobContainerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Versions = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicPlugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DynamicPluginDocumentProcesses",
                columns: table => new
                {
                    DynamicPluginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DynamicDocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicPluginDocumentProcesses", x => new { x.DynamicPluginId, x.DynamicDocumentProcessDefinitionId });
                    table.ForeignKey(
                        name: "FK_DynamicPluginDocumentProcesses_DynamicDocumentProcessDefinitions_DynamicDocumentProcessDefinitionId",
                        column: x => x.DynamicDocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DynamicPluginDocumentProcesses_DynamicPlugins_DynamicPluginId",
                        column: x => x.DynamicPluginId,
                        principalTable: "DynamicPlugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessMetaDataFields_DynamicDocumentProcessDefinitionId_Name",
                table: "DynamicDocumentProcessMetaDataFields",
                columns: new[] { "DynamicDocumentProcessDefinitionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DynamicPluginDocumentProcesses_DynamicDocumentProcessDefinitionId",
                table: "DynamicPluginDocumentProcesses",
                column: "DynamicDocumentProcessDefinitionId");
        }
    }
}
