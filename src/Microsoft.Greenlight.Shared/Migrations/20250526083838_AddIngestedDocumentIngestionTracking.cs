using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestedDocumentIngestionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_DocumentProcess",
                table: "IngestedDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "IngestedDocuments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentProcess",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Container",
                table: "IngestedDocuments",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderPath",
                table: "IngestedDocuments",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "IngestedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedUtc",
                table: "IngestedDocuments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationId",
                table: "IngestedDocuments",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IsDeleted",
                table: "IngestedDocuments",
                columns: new[] { "Container", "FolderPath", "FileName", "FileHash", "IsDeleted" },
                unique: true,
                filter: "[FileHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_OrchestrationId",
                table: "IngestedDocuments",
                column: "OrchestrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_Container_FolderPath_FileName_FileHash_IsDeleted",
                table: "IngestedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_OrchestrationId",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "Container",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "Error",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "FolderPath",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "LastUpdatedUtc",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "OrchestrationId",
                table: "IngestedDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentProcess",
                table: "IngestedDocuments",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_DocumentProcess",
                table: "IngestedDocuments",
                column: "DocumentProcess");
        }
    }
}
