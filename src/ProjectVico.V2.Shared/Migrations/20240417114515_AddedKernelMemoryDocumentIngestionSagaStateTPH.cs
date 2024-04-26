using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedKernelMemoryDocumentIngestionSagaStateTPH : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassificationSuperType",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.DropColumn(
                name: "ClassificationType",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.DropColumn(
                name: "IngestionState",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.DropColumn(
                name: "IngestionType",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.AlterColumn<string>(
                name: "FileHash",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentProcessName",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "DocumentIngestionSagaState");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIngestionSagaStates_DocumentProcessName",
                table: "DocumentIngestionSagaStates",
                column: "DocumentProcessName");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIngestionSagaStates_FileHash",
                table: "DocumentIngestionSagaStates",
                column: "FileHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentIngestionSagaStates_DocumentProcessName",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.DropIndex(
                name: "IX_DocumentIngestionSagaStates_FileHash",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "DocumentIngestionSagaStates");

            migrationBuilder.AlterColumn<string>(
                name: "FileHash",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentProcessName",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassificationSuperType",
                table: "DocumentIngestionSagaStates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassificationType",
                table: "DocumentIngestionSagaStates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IngestionState",
                table: "DocumentIngestionSagaStates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IngestionType",
                table: "DocumentIngestionSagaStates",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
