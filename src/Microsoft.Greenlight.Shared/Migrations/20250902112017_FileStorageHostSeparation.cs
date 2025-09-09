using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class FileStorageHostSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove any existing file storage source associations to avoid foreign key conflicts
            migrationBuilder.Sql("DELETE FROM [DocumentProcessFileStorageSources]");
            migrationBuilder.Sql("DELETE FROM [DocumentLibraryFileStorageSources]");
            migrationBuilder.Sql("DELETE FROM [FileAcknowledgmentRecords]");
            
            // Clear existing file storage sources - we'll recreate them via seeding
            migrationBuilder.Sql("DELETE FROM [FileStorageSources]");

            // Drop the old index before removing columns
            migrationBuilder.DropIndex(
                name: "IX_FileStorageSources_ProviderType_ContainerOrPath",
                table: "FileStorageSources");

            // Create FileStorageHosts table first
            migrationBuilder.CreateTable(
                name: "FileStorageHosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderType = table.Column<int>(type: "int", nullable: false),
                    ConnectionString = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AuthenticationKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileStorageHosts", x => x.Id);
                });

            // Add FileStorageHostId column to FileStorageSources with a default empty GUID
            // (seeding will populate proper host relationships)
            migrationBuilder.AddColumn<Guid>(
                name: "FileStorageHostId",
                table: "FileStorageSources",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Remove the old columns from FileStorageSources
            migrationBuilder.DropColumn(
                name: "ConnectionString",
                table: "FileStorageSources");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "FileStorageSources");

            // Rename AuthenticationKey to Description
            migrationBuilder.RenameColumn(
                name: "AuthenticationKey",
                table: "FileStorageSources",
                newName: "Description");

            // Make AutoImportFolderName nullable
            migrationBuilder.AlterColumn<string>(
                name: "AutoImportFolderName",
                table: "FileStorageSources",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // Create indexes for FileStorageHosts
            migrationBuilder.CreateIndex(
                name: "IX_FileStorageHosts_Name",
                table: "FileStorageHosts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileStorageHosts_ProviderType_ConnectionString",
                table: "FileStorageHosts",
                columns: new[] { "ProviderType", "ConnectionString" });

            // Create new index for FileStorageSources
            migrationBuilder.CreateIndex(
                name: "IX_FileStorageSources_FileStorageHostId_ContainerOrPath",
                table: "FileStorageSources",
                columns: new[] { "FileStorageHostId", "ContainerOrPath" },
                unique: true);

            // Add the foreign key constraint
            migrationBuilder.AddForeignKey(
                name: "FK_FileStorageSources_FileStorageHosts_FileStorageHostId",
                table: "FileStorageSources",
                column: "FileStorageHostId",
                principalTable: "FileStorageHosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileStorageSources_FileStorageHosts_FileStorageHostId",
                table: "FileStorageSources");

            migrationBuilder.DropTable(
                name: "FileStorageHosts");

            migrationBuilder.DropIndex(
                name: "IX_FileStorageSources_FileStorageHostId_ContainerOrPath",
                table: "FileStorageSources");

            migrationBuilder.DropColumn(
                name: "FileStorageHostId",
                table: "FileStorageSources");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "FileStorageSources",
                newName: "AuthenticationKey");

            migrationBuilder.AlterColumn<string>(
                name: "AutoImportFolderName",
                table: "FileStorageSources",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionString",
                table: "FileStorageSources",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProviderType",
                table: "FileStorageSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FileStorageSources_ProviderType_ContainerOrPath",
                table: "FileStorageSources",
                columns: new[] { "ProviderType", "ContainerOrPath" });
        }
    }
}
