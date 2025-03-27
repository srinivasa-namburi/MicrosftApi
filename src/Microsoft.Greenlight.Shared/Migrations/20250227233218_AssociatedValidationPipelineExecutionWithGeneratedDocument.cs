using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AssociatedValidationPipelineExecutionWithGeneratedDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedDocumentId",
                table: "ValidationPipelineExecutions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Delete ValidationPipelineExecutions with null GeneratedDocumentId
            migrationBuilder.Sql("DELETE FROM ValidationPipelineExecutions WHERE GeneratedDocumentId = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationPipelineExecutions_GeneratedDocumentId",
                table: "ValidationPipelineExecutions",
                column: "GeneratedDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ValidationPipelineExecutions_GeneratedDocuments_GeneratedDocumentId",
                table: "ValidationPipelineExecutions",
                column: "GeneratedDocumentId",
                principalTable: "GeneratedDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ValidationPipelineExecutions_GeneratedDocuments_GeneratedDocumentId",
                table: "ValidationPipelineExecutions");

            migrationBuilder.DropIndex(
                name: "IX_ValidationPipelineExecutions_GeneratedDocumentId",
                table: "ValidationPipelineExecutions");

            migrationBuilder.DropColumn(
                name: "GeneratedDocumentId",
                table: "ValidationPipelineExecutions");
        }
    }
}
