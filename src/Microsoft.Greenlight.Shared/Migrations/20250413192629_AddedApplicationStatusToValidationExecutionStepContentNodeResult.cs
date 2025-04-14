using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedApplicationStatusToValidationExecutionStepContentNodeResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionStepResults_ValidationPipelineExecutionStepResultId",
                table: "ValidationExecutionStepContentNodeResults");

            migrationBuilder.AddColumn<int>(
                name: "ApplicationStatus",
                table: "ValidationExecutionStepContentNodeResults",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_ApplicationStatus",
                table: "ValidationExecutionStepContentNodeResults",
                column: "ApplicationStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionStepResults_ValidationPipelineExecutionStepResultId",
                table: "ValidationExecutionStepContentNodeResults",
                column: "ValidationPipelineExecutionStepResultId",
                principalTable: "ValidationPipelineExecutionStepResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionStepResults_ValidationPipelineExecutionStepResultId",
                table: "ValidationExecutionStepContentNodeResults");

            migrationBuilder.DropIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_ApplicationStatus",
                table: "ValidationExecutionStepContentNodeResults");

            migrationBuilder.DropColumn(
                name: "ApplicationStatus",
                table: "ValidationExecutionStepContentNodeResults");

            migrationBuilder.AddForeignKey(
                name: "FK_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionStepResults_ValidationPipelineExecutionStepResultId",
                table: "ValidationExecutionStepContentNodeResults",
                column: "ValidationPipelineExecutionStepResultId",
                principalTable: "ValidationPipelineExecutionStepResults",
                principalColumn: "Id");
        }
    }
}
