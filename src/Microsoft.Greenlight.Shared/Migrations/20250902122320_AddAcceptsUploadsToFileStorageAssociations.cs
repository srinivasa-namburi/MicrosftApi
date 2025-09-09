using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptsUploadsToFileStorageAssociations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsUploads",
                table: "DocumentProcessFileStorageSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptsUploads",
                table: "DocumentLibraryFileStorageSources",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsUploads",
                table: "DocumentProcessFileStorageSources");

            migrationBuilder.DropColumn(
                name: "AcceptsUploads",
                table: "DocumentLibraryFileStorageSources");
        }
    }
}
