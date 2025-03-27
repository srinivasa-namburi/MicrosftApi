using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ValidationSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValidationPipelineSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidationPipelineExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedSteps = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentStepIndex = table.Column<int>(type: "int", nullable: false),
                    StepTimeoutTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationPipelineSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationPipelineSagaStates_ValidationPipelineExecutionId",
                table: "ValidationPipelineSagaStates",
                column: "ValidationPipelineExecutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValidationPipelineSagaStates");
        }
    }
}
