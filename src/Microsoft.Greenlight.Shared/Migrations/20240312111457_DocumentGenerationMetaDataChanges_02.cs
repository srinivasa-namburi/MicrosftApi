using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentGenerationMetaDataChanges_02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MetadataDefinition",
                table: "DocumentGenerationSagaStates",
                newName: "MetadataJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MetadataJson",
                table: "DocumentGenerationSagaStates",
                newName: "MetadataDefinition");
        }
    }
}
