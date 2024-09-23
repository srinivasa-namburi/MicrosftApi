using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddExportedDocumentLinkToReviewInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullBlobUrlToReviewedDocument",
                table: "ReviewInstances");

            migrationBuilder.AddColumn<Guid>(
                name: "ExportedLinkId",
                table: "ReviewInstances",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ReviewInstances_ExportedLinkId",
                table: "ReviewInstances",
                column: "ExportedLinkId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewInstances_ExportedDocumentLinks_ExportedLinkId",
                table: "ReviewInstances",
                column: "ExportedLinkId",
                principalTable: "ExportedDocumentLinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewInstances_ExportedDocumentLinks_ExportedLinkId",
                table: "ReviewInstances");

            migrationBuilder.DropIndex(
                name: "IX_ReviewInstances_ExportedLinkId",
                table: "ReviewInstances");

            migrationBuilder.DropColumn(
                name: "ExportedLinkId",
                table: "ReviewInstances");

            migrationBuilder.AddColumn<string>(
                name: "FullBlobUrlToReviewedDocument",
                table: "ReviewInstances",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
