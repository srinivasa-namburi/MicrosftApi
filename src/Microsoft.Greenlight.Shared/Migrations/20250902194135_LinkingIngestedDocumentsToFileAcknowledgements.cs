using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class LinkingIngestedDocumentsToFileAcknowledgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileAcknowledgmentRecords_IngestedDocuments_IngestedDocumentId",
                table: "FileAcknowledgmentRecords");

            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IngestionState",
                table: "IngestedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_FileAcknowledgmentRecords_IngestedDocumentId",
                table: "FileAcknowledgmentRecords");

            migrationBuilder.DropColumn(
                name: "IngestedDocumentId",
                table: "FileAcknowledgmentRecords");

            migrationBuilder.CreateTable(
                name: "IngestedDocumentFileAcknowledgments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngestedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAcknowledgmentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestedDocumentFileAcknowledgments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestedDocumentFileAcknowledgments_FileAcknowledgmentRecords_FileAcknowledgmentRecordId",
                        column: x => x.FileAcknowledgmentRecordId,
                        principalTable: "FileAcknowledgmentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngestedDocumentFileAcknowledgments_IngestedDocuments_IngestedDocumentId",
                        column: x => x.IngestedDocumentId,
                        principalTable: "IngestedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_DocumentLibraryType_DocumentLibraryOrProcessName_Container_FolderPath_FileName_FileHash",
                table: "IngestedDocuments",
                columns: new[] { "DocumentLibraryType", "DocumentLibraryOrProcessName", "Container", "FolderPath", "FileName", "FileHash" },
                unique: true,
                filter: "[DocumentLibraryOrProcessName] IS NOT NULL AND [FileHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocumentFileAcknowledgments_FileAcknowledgmentRecordId",
                table: "IngestedDocumentFileAcknowledgments",
                column: "FileAcknowledgmentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocumentFileAcknowledgments_IngestedDocumentId_FileAcknowledgmentRecordId",
                table: "IngestedDocumentFileAcknowledgments",
                columns: new[] { "IngestedDocumentId", "FileAcknowledgmentRecordId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngestedDocumentFileAcknowledgments");

            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_DocumentLibraryType_DocumentLibraryOrProcessName_Container_FolderPath_FileName_FileHash",
                table: "IngestedDocuments");

            migrationBuilder.AddColumn<Guid>(
                name: "IngestedDocumentId",
                table: "FileAcknowledgmentRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IngestionState",
                table: "IngestedDocuments",
                columns: new[] { "Container", "FolderPath", "FileName", "FileHash", "IngestionState" },
                unique: true,
                filter: "[FileHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FileAcknowledgmentRecords_IngestedDocumentId",
                table: "FileAcknowledgmentRecords",
                column: "IngestedDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileAcknowledgmentRecords_IngestedDocuments_IngestedDocumentId",
                table: "FileAcknowledgmentRecords",
                column: "IngestedDocumentId",
                principalTable: "IngestedDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
