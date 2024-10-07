using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedPromptDefinitionAndImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShortCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptImplementations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromptDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptImplementations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptImplementations_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                        column: x => x.DocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromptImplementations_PromptDefinitions_PromptDefinitionId",
                        column: x => x.PromptDefinitionId,
                        principalTable: "PromptDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptDefinitions_DeletedAt_IsActive",
                table: "PromptDefinitions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptDefinitions_IsActive",
                table: "PromptDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromptDefinitions_ShortCode",
                table: "PromptDefinitions",
                column: "ShortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptImplementations_DeletedAt_IsActive",
                table: "PromptImplementations",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptImplementations_DocumentProcessDefinitionId",
                table: "PromptImplementations",
                column: "DocumentProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptImplementations_IsActive",
                table: "PromptImplementations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromptImplementations_PromptDefinitionId_DocumentProcessDefinitionId",
                table: "PromptImplementations",
                columns: new[] { "PromptDefinitionId", "DocumentProcessDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptImplementations");

            migrationBuilder.DropTable(
                name: "PromptDefinitions");
        }
    }
}
