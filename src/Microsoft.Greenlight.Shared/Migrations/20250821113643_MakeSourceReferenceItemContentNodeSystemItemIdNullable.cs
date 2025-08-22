using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MakeSourceReferenceItemContentNodeSystemItemIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ContentNodeSystemItemId",
                table: "SourceReferenceItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "DocumentId",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                table: "SourceReferenceItems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorStoreAggregatedSourceReferenceItem_IndexName",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorStoreSourceReferenceItem_DocumentId",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorStoreSourceReferenceItem_FileName",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorStoreSourceReferenceItem_IndexName",
                table: "SourceReferenceItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VectorStoreSourceReferenceItem_Score",
                table: "SourceReferenceItems",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "VectorStoreAggregatedSourceReferenceItem_IndexName",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "VectorStoreSourceReferenceItem_DocumentId",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "VectorStoreSourceReferenceItem_FileName",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "VectorStoreSourceReferenceItem_IndexName",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "VectorStoreSourceReferenceItem_Score",
                table: "SourceReferenceItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContentNodeSystemItemId",
                table: "SourceReferenceItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
