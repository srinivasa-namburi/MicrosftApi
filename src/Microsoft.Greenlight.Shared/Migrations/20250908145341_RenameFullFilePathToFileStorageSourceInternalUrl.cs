using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenameFullFilePathToFileStorageSourceInternalUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FullFilePath",
                table: "FileAcknowledgmentRecords",
                newName: "FileStorageSourceInternalUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileStorageSourceInternalUrl",
                table: "FileAcknowledgmentRecords",
                newName: "FullFilePath");
        }
    }
}
