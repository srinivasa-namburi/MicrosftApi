// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorStoreChunkingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add nullable columns with default value 0 (Simple) so new rows default correctly
            migrationBuilder.AddColumn<int>(
                name: "VectorStoreChunkingMode",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VectorStoreChunkingMode",
                table: "DocumentLibraries",
                type: "int",
                nullable: true,
                defaultValue: 0);

            // Backfill existing rows explicitly (in case provider behavior differs) to Simple (0)
            migrationBuilder.Sql("UPDATE DynamicDocumentProcessDefinitions SET VectorStoreChunkingMode = 0 WHERE VectorStoreChunkingMode IS NULL");
            migrationBuilder.Sql("UPDATE DocumentLibraries SET VectorStoreChunkingMode = 0 WHERE VectorStoreChunkingMode IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VectorStoreChunkingMode",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "VectorStoreChunkingMode",
                table: "DocumentLibraries");
        }
    }
}
