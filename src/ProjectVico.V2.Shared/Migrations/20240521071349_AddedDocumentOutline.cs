using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedDocumentOutline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentOutlineId",
                table: "DynamicDocumentProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentOutlines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOutlines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOutlines_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                        column: x => x.DocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlines_DeletedAt_IsActive",
                table: "DocumentOutlines",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlines_DocumentProcessDefinitionId",
                table: "DocumentOutlines",
                column: "DocumentProcessDefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlines_IsActive",
                table: "DocumentOutlines",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOutlines");

            migrationBuilder.DropColumn(
                name: "DocumentOutlineId",
                table: "DynamicDocumentProcessDefinitions");
        }
    }
}
