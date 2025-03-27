using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ValidationSagaStateUpdatedToUseExecutionIdAsCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ValidationPipelineSagaStates_ValidationPipelineExecutionId",
                table: "ValidationPipelineSagaStates");

            migrationBuilder.DropColumn(
                name: "ValidationPipelineExecutionId",
                table: "ValidationPipelineSagaStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ValidationPipelineExecutionId",
                table: "ValidationPipelineSagaStates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ValidationPipelineSagaStates_ValidationPipelineExecutionId",
                table: "ValidationPipelineSagaStates",
                column: "ValidationPipelineExecutionId");
        }
    }
}
