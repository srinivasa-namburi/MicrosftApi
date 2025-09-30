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

            migrationBuilder.RenameColumn(
                name: "ExportedLinkId",
                table: "ReviewInstances",
                newName: "ExternalLinkAssetId");

            migrationBuilder.RenameIndex(
                name: "IX_ReviewInstances_ExportedLinkId",
                table: "ReviewInstances",
                newName: "IX_ReviewInstances_ExternalLinkAssetId");

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

            migrationBuilder.RenameColumn(
                name: "ExternalLinkAssetId",
                table: "ReviewInstances",
                newName: "ExportedLinkId");

            migrationBuilder.RenameIndex(
                name: "IX_ReviewInstances_ExternalLinkAssetId",
                table: "ReviewInstances",
                newName: "IX_ReviewInstances_ExportedLinkId");

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
