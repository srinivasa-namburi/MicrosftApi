using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessToReviewInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentProcessDefinitionId",
                table: "ReviewInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentProcessShortName",
                table: "ReviewInstances",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentProcessDefinitionId",
                table: "ReviewInstances");

            migrationBuilder.DropColumn(
                name: "DocumentProcessShortName",
                table: "ReviewInstances");
        }
    }
}
