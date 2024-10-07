using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentGenerationMetaDataChanges_01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentGenerationRequest",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.AddColumn<string>(
                name: "MetadataDefinition",
                table: "DocumentGenerationSagaStates",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataDefinition",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.AddColumn<string>(
                name: "DocumentGenerationRequest",
                table: "DocumentGenerationSagaStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
