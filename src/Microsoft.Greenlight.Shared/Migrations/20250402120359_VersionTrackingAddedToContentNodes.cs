using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class VersionTrackingAddedToContentNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContentNodeVersionTrackerId",
                table: "ContentNodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentNodeVersionTrackers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentNodeVersionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    ContentNodeType = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentNodeVersionTrackers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_ContentNodeVersionTrackerId",
                table: "ContentNodes",
                column: "ContentNodeVersionTrackerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodeVersionTrackers_ContentNodeId",
                table: "ContentNodeVersionTrackers",
                column: "ContentNodeId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentNodes_ContentNodeVersionTrackers_ContentNodeVersionTrackerId",
                table: "ContentNodes",
                column: "ContentNodeVersionTrackerId",
                principalTable: "ContentNodeVersionTrackers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentNodes_ContentNodeVersionTrackers_ContentNodeVersionTrackerId",
                table: "ContentNodes");

            migrationBuilder.DropTable(
                name: "ContentNodeVersionTrackers");

            migrationBuilder.DropIndex(
                name: "IX_ContentNodes_ContentNodeVersionTrackerId",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "ContentNodeVersionTrackerId",
                table: "ContentNodes");
        }
    }
}
