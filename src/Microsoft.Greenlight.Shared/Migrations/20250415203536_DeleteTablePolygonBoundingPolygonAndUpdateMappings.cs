using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DeleteTablePolygonBoundingPolygonAndUpdateMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentNodes_IngestedDocuments_IngestedDocumentId",
                table: "ContentNodes");

            migrationBuilder.DropTable(
                name: "BoundingPolygons");

            migrationBuilder.DropTable(
                name: "TableCells");

            migrationBuilder.DropTable(
                name: "BoundingRegions");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_ContentNodes_IngestedDocumentId",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "ClassificationType",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "IngestedDocumentId",
                table: "ContentNodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassificationType",
                table: "IngestedDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IngestedDocumentId",
                table: "ContentNodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngestedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ColumnCount = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_IngestedDocuments_IngestedDocumentId",
                        column: x => x.IngestedDocumentId,
                        principalTable: "IngestedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoundingRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Page = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoundingRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoundingRegions_ContentNodes_ContentNodeId",
                        column: x => x.ContentNodeId,
                        principalTable: "ContentNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BoundingRegions_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TableCells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnIndex = table.Column<int>(type: "int", nullable: false),
                    ColumnSpan = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowIndex = table.Column<int>(type: "int", nullable: false),
                    RowSpan = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableCells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TableCells_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoundingPolygons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoundingRegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsEmpty = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    X = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: false),
                    Y = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoundingPolygons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoundingPolygons_BoundingRegions_BoundingRegionId",
                        column: x => x.BoundingRegionId,
                        principalTable: "BoundingRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_IngestedDocumentId",
                table: "ContentNodes",
                column: "IngestedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingPolygons_BoundingRegionId",
                table: "BoundingPolygons",
                column: "BoundingRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingPolygons_X_Y",
                table: "BoundingPolygons",
                columns: new[] { "X", "Y" });

            migrationBuilder.CreateIndex(
                name: "IX_BoundingRegions_ContentNodeId",
                table: "BoundingRegions",
                column: "ContentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingRegions_Page",
                table: "BoundingRegions",
                column: "Page");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingRegions_TableId",
                table: "BoundingRegions",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_TableCells_TableId",
                table: "TableCells",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_IngestedDocumentId",
                table: "Tables",
                column: "IngestedDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentNodes_IngestedDocuments_IngestedDocumentId",
                table: "ContentNodes",
                column: "IngestedDocumentId",
                principalTable: "IngestedDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
