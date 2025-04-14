using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedAdditionalIndexToValidationExecutionStepContentNodeResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_OriginalContentNodeId_ResultantContentNodeId",
                table: "ValidationExecutionStepContentNodeResults",
                columns: new[] { "OriginalContentNodeId", "ResultantContentNodeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_OriginalContentNodeId_ResultantContentNodeId",
                table: "ValidationExecutionStepContentNodeResults");
        }
    }
}
