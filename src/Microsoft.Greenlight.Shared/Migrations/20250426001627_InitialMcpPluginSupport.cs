using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class InitialMcpPluginSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpPlugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobContainerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpPlugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpPluginVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Major = table.Column<int>(type: "int", nullable: false),
                    Minor = table.Column<int>(type: "int", nullable: false),
                    Patch = table.Column<int>(type: "int", nullable: false),
                    McpPluginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Command = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Arguments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpPluginVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpPluginVersions_McpPlugins_McpPluginId",
                        column: x => x.McpPluginId,
                        principalTable: "McpPlugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpPluginDocumentProcesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    McpPluginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DynamicDocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpPluginDocumentProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpPluginDocumentProcesses_DynamicDocumentProcessDefinitions_DynamicDocumentProcessDefinitionId",
                        column: x => x.DynamicDocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_McpPluginDocumentProcesses_McpPluginVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "McpPluginVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_McpPluginDocumentProcesses_McpPlugins_McpPluginId",
                        column: x => x.McpPluginId,
                        principalTable: "McpPlugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpPluginDocumentProcesses_DynamicDocumentProcessDefinitionId",
                table: "McpPluginDocumentProcesses",
                column: "DynamicDocumentProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_McpPluginDocumentProcesses_McpPluginId_DynamicDocumentProcessDefinitionId",
                table: "McpPluginDocumentProcesses",
                columns: new[] { "McpPluginId", "DynamicDocumentProcessDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_McpPluginDocumentProcesses_VersionId",
                table: "McpPluginDocumentProcesses",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_McpPlugins_Name",
                table: "McpPlugins",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpPluginVersions_McpPluginId_Major_Minor_Patch",
                table: "McpPluginVersions",
                columns: new[] { "McpPluginId", "Major", "Minor", "Patch" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpPluginDocumentProcesses");

            migrationBuilder.DropTable(
                name: "McpPluginVersions");

            migrationBuilder.DropTable(
                name: "McpPlugins");
        }
    }
}
