using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ContentReferenceUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StorageSourceDataType",
                table: "FileStorageSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContentReferenceTypeFileStorageSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentReferenceType = table.Column<int>(type: "int", nullable: false),
                    FileStorageSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AcceptsUploads = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReferenceTypeFileStorageSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentReferenceTypeFileStorageSources_FileStorageSources_FileStorageSourceId",
                        column: x => x.FileStorageSourceId,
                        principalTable: "FileStorageSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileStorageSourceCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileStorageSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileStorageSourceCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileStorageSourceCategories_FileStorageSources_FileStorageSourceId",
                        column: x => x.FileStorageSourceId,
                        principalTable: "FileStorageSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceTypeFileStorageSources_ContentReferenceType_FileStorageSourceId",
                table: "ContentReferenceTypeFileStorageSources",
                columns: new[] { "ContentReferenceType", "FileStorageSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReferenceTypeFileStorageSources_FileStorageSourceId",
                table: "ContentReferenceTypeFileStorageSources",
                column: "FileStorageSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FileStorageSourceCategories_FileStorageSourceId_DataType",
                table: "FileStorageSourceCategories",
                columns: new[] { "FileStorageSourceId", "DataType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentReferenceTypeFileStorageSources");

            migrationBuilder.DropTable(
                name: "FileStorageSourceCategories");

            migrationBuilder.DropColumn(
                name: "StorageSourceDataType",
                table: "FileStorageSources");
        }
    }
}
