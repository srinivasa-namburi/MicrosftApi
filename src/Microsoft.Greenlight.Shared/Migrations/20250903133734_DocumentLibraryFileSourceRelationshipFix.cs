using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentLibraryFileSourceRelationshipFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLibraryFileStorageSources_DocumentLibraries_DocumentLibraryId1",
                table: "DocumentLibraryFileStorageSources");

            migrationBuilder.DropIndex(
                name: "IX_DocumentLibraryFileStorageSources_DocumentLibraryId1",
                table: "DocumentLibraryFileStorageSources");

            migrationBuilder.DropColumn(
                name: "DocumentLibraryId1",
                table: "DocumentLibraryFileStorageSources");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentLibraryId1",
                table: "DocumentLibraryFileStorageSources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraryFileStorageSources_DocumentLibraryId1",
                table: "DocumentLibraryFileStorageSources",
                column: "DocumentLibraryId1");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLibraryFileStorageSources_DocumentLibraries_DocumentLibraryId1",
                table: "DocumentLibraryFileStorageSources",
                column: "DocumentLibraryId1",
                principalTable: "DocumentLibraries",
                principalColumn: "Id");
        }
    }
}
