using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedDocumentOutlineItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentOutlineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionNumber = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SectionTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentOutlineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOutlineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineItems_DocumentOutlineItems_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DocumentOutlineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineItems_DocumentOutlines_DocumentOutlineId",
                        column: x => x.DocumentOutlineId,
                        principalTable: "DocumentOutlines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineItems_DocumentOutlineId",
                table: "DocumentOutlineItems",
                column: "DocumentOutlineId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineItems_ParentId",
                table: "DocumentOutlineItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineItems_SectionNumber",
                table: "DocumentOutlineItems",
                column: "SectionNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOutlineItems");
        }
    }
}
