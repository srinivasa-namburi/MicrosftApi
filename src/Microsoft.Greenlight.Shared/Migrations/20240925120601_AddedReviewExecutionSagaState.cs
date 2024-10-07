using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedReviewExecutionSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewExecutionSagaStates",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExportedDocumentLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalNumberOfQuestions = table.Column<int>(type: "int", nullable: false),
                    NumberOfQuestionsAnswered = table.Column<int>(type: "int", nullable: false),
                    NumberOfQuestionsAnsweredWithSentiment = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewExecutionSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewExecutionSagaStates_ExportedDocumentLinkId",
                table: "ReviewExecutionSagaStates",
                column: "ExportedDocumentLinkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewExecutionSagaStates");
        }
    }
}
