using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddFileStorageSourceIdToExternalLinkAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileStorageSourceId",
                table: "ExternalLinkAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLinkAssets_FileStorageSourceId",
                table: "ExternalLinkAssets",
                column: "FileStorageSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalLinkAssets_FileStorageSources_FileStorageSourceId",
                table: "ExternalLinkAssets",
                column: "FileStorageSourceId",
                principalTable: "FileStorageSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalLinkAssets_FileStorageSources_FileStorageSourceId",
                table: "ExternalLinkAssets");

            migrationBuilder.DropIndex(
                name: "IX_ExternalLinkAssets_FileStorageSourceId",
                table: "ExternalLinkAssets");

            migrationBuilder.DropColumn(
                name: "FileStorageSourceId",
                table: "ExternalLinkAssets");
        }
    }
}
