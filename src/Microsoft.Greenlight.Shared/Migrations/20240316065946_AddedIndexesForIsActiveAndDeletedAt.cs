using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedIndexesForIsActiveAndDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tables_DeletedAt_IsActive",
                table: "Tables",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Tables_IsActive",
                table: "Tables",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TableCells_DeletedAt_IsActive",
                table: "TableCells",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TableCells_IsActive",
                table: "TableCells",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_DeletedAt_IsActive",
                table: "IngestedDocuments",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_IsActive",
                table: "IngestedDocuments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_DeletedAt_IsActive",
                table: "GeneratedDocuments",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_IsActive",
                table: "GeneratedDocuments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_DeletedAt_IsActive",
                table: "DocumentMetadata",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_IsActive",
                table: "DocumentMetadata",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_DeletedAt_IsActive",
                table: "ContentNodes",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_IsActive",
                table: "ContentNodes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingRegions_DeletedAt_IsActive",
                table: "BoundingRegions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BoundingRegions_IsActive",
                table: "BoundingRegions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingPolygons_DeletedAt_IsActive",
                table: "BoundingPolygons",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BoundingPolygons_IsActive",
                table: "BoundingPolygons",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_DeletedAt_IsActive",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_IsActive",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_TableCells_DeletedAt_IsActive",
                table: "TableCells");

            migrationBuilder.DropIndex(
                name: "IX_TableCells_IsActive",
                table: "TableCells");

            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_DeletedAt_IsActive",
                table: "IngestedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_IsActive",
                table: "IngestedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedDocuments_DeletedAt_IsActive",
                table: "GeneratedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedDocuments_IsActive",
                table: "GeneratedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_DocumentMetadata_DeletedAt_IsActive",
                table: "DocumentMetadata");

            migrationBuilder.DropIndex(
                name: "IX_DocumentMetadata_IsActive",
                table: "DocumentMetadata");

            migrationBuilder.DropIndex(
                name: "IX_ContentNodes_DeletedAt_IsActive",
                table: "ContentNodes");

            migrationBuilder.DropIndex(
                name: "IX_ContentNodes_IsActive",
                table: "ContentNodes");

            migrationBuilder.DropIndex(
                name: "IX_BoundingRegions_DeletedAt_IsActive",
                table: "BoundingRegions");

            migrationBuilder.DropIndex(
                name: "IX_BoundingRegions_IsActive",
                table: "BoundingRegions");

            migrationBuilder.DropIndex(
                name: "IX_BoundingPolygons_DeletedAt_IsActive",
                table: "BoundingPolygons");

            migrationBuilder.DropIndex(
                name: "IX_BoundingPolygons_IsActive",
                table: "BoundingPolygons");
        }
    }
}
