using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddFileStorageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileStorageSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderType = table.Column<int>(type: "int", nullable: false),
                    ConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainerOrPath = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AutoImportFolderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AuthenticationKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShouldMoveFiles = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileStorageSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLibraryFileStorageSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentLibraryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileStorageSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DocumentLibraryId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLibraryFileStorageSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLibraryFileStorageSources_DocumentLibraries_DocumentLibraryId",
                        column: x => x.DocumentLibraryId,
                        principalTable: "DocumentLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentLibraryFileStorageSources_DocumentLibraries_DocumentLibraryId1",
                        column: x => x.DocumentLibraryId1,
                        principalTable: "DocumentLibraries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentLibraryFileStorageSources_FileStorageSources_FileStorageSourceId",
                        column: x => x.FileStorageSourceId,
                        principalTable: "FileStorageSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentProcessFileStorageSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileStorageSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DynamicDocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentProcessFileStorageSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentProcessFileStorageSources_DynamicDocumentProcessDefinitions_DocumentProcessId",
                        column: x => x.DocumentProcessId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentProcessFileStorageSources_DynamicDocumentProcessDefinitions_DynamicDocumentProcessDefinitionId",
                        column: x => x.DynamicDocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentProcessFileStorageSources_FileStorageSources_FileStorageSourceId",
                        column: x => x.FileStorageSourceId,
                        principalTable: "FileStorageSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileAcknowledgmentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileStorageSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelativeFilePath = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullFilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcknowledgedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IngestedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAcknowledgmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileAcknowledgmentRecords_FileStorageSources_FileStorageSourceId",
                        column: x => x.FileStorageSourceId,
                        principalTable: "FileStorageSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileAcknowledgmentRecords_IngestedDocuments_IngestedDocumentId",
                        column: x => x.IngestedDocumentId,
                        principalTable: "IngestedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraryFileStorageSources_DocumentLibraryId_FileStorageSourceId",
                table: "DocumentLibraryFileStorageSources",
                columns: new[] { "DocumentLibraryId", "FileStorageSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraryFileStorageSources_DocumentLibraryId1",
                table: "DocumentLibraryFileStorageSources",
                column: "DocumentLibraryId1");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLibraryFileStorageSources_FileStorageSourceId",
                table: "DocumentLibraryFileStorageSources",
                column: "FileStorageSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessFileStorageSources_DocumentProcessId_FileStorageSourceId",
                table: "DocumentProcessFileStorageSources",
                columns: new[] { "DocumentProcessId", "FileStorageSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessFileStorageSources_DynamicDocumentProcessDefinitionId",
                table: "DocumentProcessFileStorageSources",
                column: "DynamicDocumentProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessFileStorageSources_FileStorageSourceId",
                table: "DocumentProcessFileStorageSources",
                column: "FileStorageSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FileAcknowledgmentRecords_FileStorageSourceId_RelativeFilePath",
                table: "FileAcknowledgmentRecords",
                columns: new[] { "FileStorageSourceId", "RelativeFilePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileAcknowledgmentRecords_IngestedDocumentId",
                table: "FileAcknowledgmentRecords",
                column: "IngestedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_FileStorageSources_Name",
                table: "FileStorageSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileStorageSources_ProviderType_ContainerOrPath",
                table: "FileStorageSources",
                columns: new[] { "ProviderType", "ContainerOrPath" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentLibraryFileStorageSources");

            migrationBuilder.DropTable(
                name: "DocumentProcessFileStorageSources");

            migrationBuilder.DropTable(
                name: "FileAcknowledgmentRecords");

            migrationBuilder.DropTable(
                name: "FileStorageSources");
        }
    }
}
