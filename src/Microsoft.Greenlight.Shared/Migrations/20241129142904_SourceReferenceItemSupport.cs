using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class SourceReferenceItemSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContentNodeSystemItemId",
                table: "ContentNodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentNodeSystemItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComputedUsedMainGenerationPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComputedSectionPromptInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentNodeSystemItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentNodeSystemItems_ContentNodes_ContentNodeId",
                        column: x => x.ContentNodeId,
                        principalTable: "ContentNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceReferenceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentNodeSystemItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReferenceType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceReferenceLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceReferenceLinkType = table.Column<int>(type: "int", nullable: true),
                    SourceOutput = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Discriminator = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IndexName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CitationJsons = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentLibraryShortName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentProcessShortName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PluginIdentifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceReferenceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceReferenceItems_ContentNodeSystemItems_ContentNodeSystemItemId",
                        column: x => x.ContentNodeSystemItemId,
                        principalTable: "ContentNodeSystemItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodeSystemItems_ContentNodeId",
                table: "ContentNodeSystemItems",
                column: "ContentNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferenceItems_ContentNodeSystemItemId",
                table: "SourceReferenceItems",
                column: "ContentNodeSystemItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceReferenceItems");

            migrationBuilder.DropTable(
                name: "ContentNodeSystemItems");

            migrationBuilder.DropColumn(
                name: "ContentNodeSystemItemId",
                table: "ContentNodes");
        }
    }
}
