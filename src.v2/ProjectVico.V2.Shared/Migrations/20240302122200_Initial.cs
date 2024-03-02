using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentGenerationSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorOid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReactorModel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationLatitude = table.Column<double>(type: "float", nullable: true),
                    LocationLongitude = table.Column<double>(type: "float", nullable: true),
                    ProjectedProjectStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProjectedProjectEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NumberOfContentNodesToGenerate = table.Column<int>(type: "int", nullable: false),
                    NumberOfContentNodesGenerated = table.Column<int>(type: "int", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGenerationSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "DocumentIngestionSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalDocumentUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedByUserOid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassificationShortCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassificationType = table.Column<int>(type: "int", nullable: true),
                    ClassificationSuperType = table.Column<int>(type: "int", nullable: true),
                    IngestionState = table.Column<int>(type: "int", nullable: false),
                    IngestionType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentIngestionSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestingAuthorOid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngestedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    OriginalDocumentUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedByUserOid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IngestionType = table.Column<int>(type: "int", nullable: false),
                    IngestionState = table.Column<int>(type: "int", nullable: false),
                    ClassificationShortCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassificationType = table.Column<int>(type: "int", nullable: true),
                    IngestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestedDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    GenerationState = table.Column<int>(type: "int", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IngestedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GeneratedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentNodes_ContentNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ContentNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentNodes_GeneratedDocuments_GeneratedDocumentId",
                        column: x => x.GeneratedDocumentId,
                        principalTable: "GeneratedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentNodes_IngestedDocuments_IngestedDocumentId",
                        column: x => x.IngestedDocumentId,
                        principalTable: "IngestedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    ColumnCount = table.Column<int>(type: "int", nullable: false),
                    IngestedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    Page = table.Column<int>(type: "int", nullable: false),
                    ContentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    RowIndex = table.Column<int>(type: "int", nullable: false),
                    ColumnIndex = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    IsEmpty = table.Column<bool>(type: "bit", nullable: false),
                    X = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: false),
                    Y = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: false),
                    BoundingRegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                name: "IX_ContentNodes_GeneratedDocumentId",
                table: "ContentNodes",
                column: "GeneratedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_IngestedDocumentId",
                table: "ContentNodes",
                column: "IngestedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_ParentId",
                table: "ContentNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_FileHash",
                table: "IngestedDocuments",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_TableCells_TableId",
                table: "TableCells",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_IngestedDocumentId",
                table: "Tables",
                column: "IngestedDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoundingPolygons");

            migrationBuilder.DropTable(
                name: "DocumentGenerationSagaStates");

            migrationBuilder.DropTable(
                name: "DocumentIngestionSagaStates");

            migrationBuilder.DropTable(
                name: "TableCells");

            migrationBuilder.DropTable(
                name: "BoundingRegions");

            migrationBuilder.DropTable(
                name: "ContentNodes");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "GeneratedDocuments");

            migrationBuilder.DropTable(
                name: "IngestedDocuments");
        }
    }
}
