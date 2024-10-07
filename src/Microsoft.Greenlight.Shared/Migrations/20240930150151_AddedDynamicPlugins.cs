using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedDynamicPlugins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DynamicPlugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobContainerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Versions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "IX_DynamicPluginDocumentProcesses_DynamicDocumentProcessDefinitionId",
                table: "DynamicPluginDocumentProcesses",
                column: "DynamicDocumentProcessDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DynamicPluginDocumentProcesses");

            migrationBuilder.DropTable(
                name: "DynamicPlugins");
        }
    }
}
