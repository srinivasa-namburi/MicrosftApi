using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedDocumentLibrariesAndDocumentLibraryProcessAssociations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentLibraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DescriptionOfContents = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionOfWhenToUse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IndexName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BlobStorageContainerName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BlobStorageAutoImportFolderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLibraries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLibraryDocumentProcessAssociations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentLibraryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DynamicDocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLibraryDocumentProcessAssociations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLibraryDocumentProcessAssociations_DocumentLibraries_DocumentLibraryId",
                        column: x => x.DocumentLibraryId,
                        principalTable: "DocumentLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentLibraryDocumentProcessAssociations_DynamicDocumentProcessDefinitions_DynamicDocumentProcessDefinitionId",
                        column: x => x.DynamicDocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraries_BlobStorageContainerName",
                table: "DocumentLibraries",
                column: "BlobStorageContainerName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraries_IndexName",
                table: "DocumentLibraries",
                column: "IndexName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraries_ShortName",
                table: "DocumentLibraries",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraryDocumentProcessAssociations_DocumentLibraryId",
                table: "DocumentLibraryDocumentProcessAssociations",
                column: "DocumentLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraryDocumentProcessAssociations_DocumentLibraryId_DynamicDocumentProcessDefinitionId",
                table: "DocumentLibraryDocumentProcessAssociations",
                columns: new[] { "DocumentLibraryId", "DynamicDocumentProcessDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraryDocumentProcessAssociations_DynamicDocumentProcessDefinitionId",
                table: "DocumentLibraryDocumentProcessAssociations",
                column: "DynamicDocumentProcessDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentLibraryDocumentProcessAssociations");

            migrationBuilder.DropTable(
                name: "DocumentLibraries");
        }
    }
}
