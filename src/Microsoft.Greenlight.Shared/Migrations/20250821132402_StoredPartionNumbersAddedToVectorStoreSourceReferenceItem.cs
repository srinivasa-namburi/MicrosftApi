using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class StoredPartionNumbersAddedToVectorStoreSourceReferenceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoredPartitionNumbers",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorStoreSourceReferenceItem_StoredPartitionNumbers",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoredPartitionNumbers",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "VectorStoreSourceReferenceItem_StoredPartitionNumbers",
                table: "SourceReferenceItems");
        }
    }
}
