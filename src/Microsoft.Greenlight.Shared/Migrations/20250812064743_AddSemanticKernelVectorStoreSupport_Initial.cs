using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddSemanticKernelVectorStoreSupport_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVectorStoreIndexed",
                table: "IngestedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VectorStoreChunkCount",
                table: "IngestedDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VectorStoreDocumentId",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorStoreIndexName",
                table: "IngestedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VectorStoreIndexedDate",
                table: "IngestedDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VectorStoreChunkOverlap",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VectorStoreChunkSize",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: true);

            // Add LogicType for DocumentLibraries. All existing libraries should be treated as KernelMemory (100).
            migrationBuilder.AddColumn<int>(
                name: "LogicType",
                table: "DocumentLibraries",
                type: "int",
                nullable: false,
                defaultValue: 100);

            // Backfill any pre-existing rows (in case default didn't apply due to explicit inserts) to KernelMemory (100)
            migrationBuilder.Sql("UPDATE DocumentLibraries SET LogicType = 100 WHERE LogicType IS NULL OR LogicType = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVectorStoreIndexed",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "VectorStoreChunkCount",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "VectorStoreDocumentId",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "VectorStoreIndexName",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "VectorStoreIndexedDate",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "VectorStoreChunkOverlap",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "VectorStoreChunkSize",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "LogicType",
                table: "DocumentLibraries");
        }
    }
}
