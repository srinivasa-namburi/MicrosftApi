using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class CompletionServiceTypeForDynamicDocumentProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletionServiceType",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 100); // DocumentProcessCompletionServiceType.GenericAiCompletionService
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionServiceType",
                table: "DynamicDocumentProcessDefinitions");
        }
    }
}