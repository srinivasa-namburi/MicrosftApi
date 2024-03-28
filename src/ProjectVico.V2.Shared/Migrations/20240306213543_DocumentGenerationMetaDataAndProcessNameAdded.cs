using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentGenerationMetaDataAndProcessNameAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentProcess",
                table: "GeneratedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MetadataId",
                table: "GeneratedDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentMetadata_GeneratedDocuments_GeneratedDocumentId",
                        column: x => x.GeneratedDocumentId,
                        principalTable: "GeneratedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_GeneratedDocumentId",
                table: "DocumentMetadata",
                column: "GeneratedDocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "DocumentProcess",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "MetadataId",
                table: "GeneratedDocuments");
        }
    }
}
