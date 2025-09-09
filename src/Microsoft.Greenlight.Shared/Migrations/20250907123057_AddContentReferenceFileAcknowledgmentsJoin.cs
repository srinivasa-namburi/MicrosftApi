using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddContentReferenceFileAcknowledgmentsJoin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentReferenceFileAcknowledgments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentReferenceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAcknowledgmentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReferenceFileAcknowledgments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentReferenceFileAcknowledgments_ContentReferenceItems_ContentReferenceItemId",
                        column: x => x.ContentReferenceItemId,
                        principalTable: "ContentReferenceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentReferenceFileAcknowledgments_FileAcknowledgmentRecords_FileAcknowledgmentRecordId",
                        column: x => x.FileAcknowledgmentRecordId,
                        principalTable: "FileAcknowledgmentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentReferenceVectorDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentReferenceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceType = table.Column<int>(type: "int", nullable: false),
                    VectorStoreIndexName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VectorStoreDocumentId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ChunkCount = table.Column<int>(type: "int", nullable: false),
                    IndexedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsIndexed = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReferenceVectorDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceFileAcknowledgments_ContentReferenceItemId_FileAcknowledgmentRecordId",
                table: "ContentReferenceFileAcknowledgments",
                columns: new[] { "ContentReferenceItemId", "FileAcknowledgmentRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceFileAcknowledgments_FileAcknowledgmentRecordId",
                table: "ContentReferenceFileAcknowledgments",
                column: "FileAcknowledgmentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceVectorDocuments_ContentReferenceItemId_VectorStoreIndexName",
                table: "ContentReferenceVectorDocuments",
                columns: new[] { "ContentReferenceItemId", "VectorStoreIndexName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentReferenceFileAcknowledgments");

            migrationBuilder.DropTable(
                name: "ContentReferenceVectorDocuments");
        }
    }
}
