using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RemovedSagaStateObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentGenerationSagaStates");

            migrationBuilder.DropTable(
                name: "DocumentIngestionSagaStates");

            migrationBuilder.DropTable(
                name: "ReviewExecutionSagaStates");

            migrationBuilder.DropTable(
                name: "ValidationPipelineSagaStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentGenerationSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorOid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentProcessName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumberOfContentNodesGenerated = table.Column<int>(type: "int", nullable: false),
                    NumberOfContentNodesToGenerate = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGenerationSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "DocumentIngestionSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassificationShortCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Discriminator = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentLibraryShortName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FileHash = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalDocumentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Plugin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedByUserOid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentLibraryType = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentIngestionSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "ReviewExecutionSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExportedDocumentLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumberOfQuestionsAnswered = table.Column<int>(type: "int", nullable: false),
                    NumberOfQuestionsAnsweredWithSentiment = table.Column<int>(type: "int", nullable: false),
                    TotalNumberOfQuestions = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewExecutionSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "ValidationPipelineSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentStepIndex = table.Column<int>(type: "int", nullable: false),
                    GeneratedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedSteps = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationPipelineSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIngestionSagaStates_DocumentLibraryShortName",
                table: "DocumentIngestionSagaStates",
                column: "DocumentLibraryShortName");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIngestionSagaStates_FileHash",
                table: "DocumentIngestionSagaStates",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewExecutionSagaStates_ExportedDocumentLinkId",
                table: "ReviewExecutionSagaStates",
                column: "ExportedDocumentLinkId");
        }
    }
}
