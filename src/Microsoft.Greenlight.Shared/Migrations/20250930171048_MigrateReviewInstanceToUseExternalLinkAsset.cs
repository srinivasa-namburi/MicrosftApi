using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MigrateReviewInstanceToUseExternalLinkAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalLinkAssetId",
                table: "ReviewInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewInstances_ExternalLinkAssetId",
                table: "ReviewInstances",
                column: "ExternalLinkAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewInstances_ExternalLinkAssets_ExternalLinkAssetId",
                table: "ReviewInstances",
                column: "ExternalLinkAssetId",
                principalTable: "ExternalLinkAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewInstances_ExternalLinkAssets_ExternalLinkAssetId",
                table: "ReviewInstances");

            migrationBuilder.DropIndex(
                name: "IX_ReviewInstances_ExternalLinkAssetId",
                table: "ReviewInstances");

            migrationBuilder.DropColumn(
                name: "ExternalLinkAssetId",
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
    }
}
