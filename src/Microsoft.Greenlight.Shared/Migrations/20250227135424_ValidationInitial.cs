using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ValidationInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ValidationPipelineId",
                table: "DynamicDocumentProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentProcessValidationPipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentProcessValidationPipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentProcessValidationPipelineSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentProcessValidationPipelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineExecutionType = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentProcessValidationPipelineSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentProcessValidationPipelineSteps_DocumentProcessValidationPipelines_DocumentProcessValidationPipelineId",
                        column: x => x.DocumentProcessValidationPipelineId,
                        principalTable: "DocumentProcessValidationPipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationPipelineExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentProcessValidationPipelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationPipelineExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationPipelineExecutions_DocumentProcessValidationPipelines_DocumentProcessValidationPipelineId",
                        column: x => x.DocumentProcessValidationPipelineId,
                        principalTable: "DocumentProcessValidationPipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationPipelineExecutionSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidationPipelineExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineExecutionType = table.Column<int>(type: "int", nullable: false),
                    PipelineExecutionStepStatus = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    RenderedFullDocumentTextForStep = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidationPipelineExecutionStepResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationPipelineExecutionSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationPipelineExecutionSteps_ValidationPipelineExecutions_ValidationPipelineExecutionId",
                        column: x => x.ValidationPipelineExecutionId,
                        principalTable: "ValidationPipelineExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationPipelineExecutionStepResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidationPipelineExecutionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationPipelineExecutionStepResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationPipelineExecutionStepResults_ValidationPipelineExecutionSteps_ValidationPipelineExecutionStepId",
                        column: x => x.ValidationPipelineExecutionStepId,
                        principalTable: "ValidationPipelineExecutionSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationExecutionStepContentNodeResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidationPipelineExecutionStepResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidationPipelineExecutionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalContentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultantContentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationExecutionStepContentNodeResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationExecutionStepContentNodeResults_ContentNodes_OriginalContentNodeId",
                        column: x => x.OriginalContentNodeId,
                        principalTable: "ContentNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ValidationExecutionStepContentNodeResults_ContentNodes_ResultantContentNodeId",
                        column: x => x.ResultantContentNodeId,
                        principalTable: "ContentNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionStepResults_ValidationPipelineExecutionStepResultId",
                        column: x => x.ValidationPipelineExecutionStepResultId,
                        principalTable: "ValidationPipelineExecutionStepResults",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionSteps_ValidationPipelineExecutionStepId",
                        column: x => x.ValidationPipelineExecutionStepId,
                        principalTable: "ValidationPipelineExecutionSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_ValidationPipelineId",
                table: "DynamicDocumentProcessDefinitions",
                column: "ValidationPipelineId",
                unique: true,
                filter: "[ValidationPipelineId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessValidationPipelines_DocumentProcessId",
                table: "DocumentProcessValidationPipelines",
                column: "DocumentProcessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProcessValidationPipelineSteps_DocumentProcessValidationPipelineId_Order",
                table: "DocumentProcessValidationPipelineSteps",
                columns: new[] { "DocumentProcessValidationPipelineId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_OriginalContentNodeId",
                table: "ValidationExecutionStepContentNodeResults",
                column: "OriginalContentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_ResultantContentNodeId",
                table: "ValidationExecutionStepContentNodeResults",
                column: "ResultantContentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionStepId",
                table: "ValidationExecutionStepContentNodeResults",
                column: "ValidationPipelineExecutionStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationExecutionStepContentNodeResults_ValidationPipelineExecutionStepResultId",
                table: "ValidationExecutionStepContentNodeResults",
                column: "ValidationPipelineExecutionStepResultId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationPipelineExecutions_DocumentProcessValidationPipelineId",
                table: "ValidationPipelineExecutions",
                column: "DocumentProcessValidationPipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationPipelineExecutionStepResults_ValidationPipelineExecutionStepId",
                table: "ValidationPipelineExecutionStepResults",
                column: "ValidationPipelineExecutionStepId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationPipelineExecutionSteps_ValidationPipelineExecutionId_Order",
                table: "ValidationPipelineExecutionSteps",
                columns: new[] { "ValidationPipelineExecutionId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_DocumentProcessValidationPipelines_ValidationPipelineId",
                table: "DynamicDocumentProcessDefinitions",
                column: "ValidationPipelineId",
                principalTable: "DocumentProcessValidationPipelines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_DocumentProcessValidationPipelines_ValidationPipelineId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropTable(
                name: "DocumentProcessValidationPipelineSteps");

            migrationBuilder.DropTable(
                name: "ValidationExecutionStepContentNodeResults");

            migrationBuilder.DropTable(
                name: "ValidationPipelineExecutionStepResults");

            migrationBuilder.DropTable(
                name: "ValidationPipelineExecutionSteps");

            migrationBuilder.DropTable(
                name: "ValidationPipelineExecutions");

            migrationBuilder.DropTable(
                name: "DocumentProcessValidationPipelines");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_ValidationPipelineId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ValidationPipelineId",
                table: "DynamicDocumentProcessDefinitions");
        }
    }
}
