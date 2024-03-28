using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentProcessAddedToIngestionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IngestionType",
                table: "IngestedDocuments");

            migrationBuilder.AddColumn<string>(
                name: "DocumentProcess",
                table: "IngestedDocuments",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentProcessName",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_DocumentProcess",
                table: "IngestedDocuments",
                column: "DocumentProcess");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_DocumentProcess",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentProcess",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentProcessName",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.AddColumn<int>(
                name: "IngestionType",
                table: "IngestedDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
