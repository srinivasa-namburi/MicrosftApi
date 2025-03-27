using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AssociatedGeneratedDocumentIdForContentNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssociatedGeneratedDocumentId",
                table: "ContentNodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_AssociatedGeneratedDocumentId",
                table: "ContentNodes",
                column: "AssociatedGeneratedDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentNodes_GeneratedDocuments_AssociatedGeneratedDocumentId",
                table: "ContentNodes",
                column: "AssociatedGeneratedDocumentId",
                principalTable: "GeneratedDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentNodes_GeneratedDocuments_AssociatedGeneratedDocumentId",
                table: "ContentNodes");

            migrationBuilder.DropIndex(
                name: "IX_ContentNodes_AssociatedGeneratedDocumentId",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "AssociatedGeneratedDocumentId",
                table: "ContentNodes");
        }
    }
}
