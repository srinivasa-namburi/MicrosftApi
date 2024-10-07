using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedReviewQuestionAnswerModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewQuestionAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalReviewQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalReviewQuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalReviewQuestionType = table.Column<int>(type: "int", nullable: false),
                    FullAiAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiSentiment = table.Column<int>(type: "int", nullable: true),
                    AiSentimentReasoning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewQuestionAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewQuestionAnswers_ReviewInstances_ReviewInstanceId",
                        column: x => x.ReviewInstanceId,
                        principalTable: "ReviewInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReviewQuestionAnswers_ReviewQuestions_OriginalReviewQuestionId",
                        column: x => x.OriginalReviewQuestionId,
                        principalTable: "ReviewQuestions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewQuestionAnswers_OriginalReviewQuestionId",
                table: "ReviewQuestionAnswers",
                column: "OriginalReviewQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewQuestionAnswers_ReviewInstanceId",
                table: "ReviewQuestionAnswers",
                column: "ReviewInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewQuestionAnswers");
        }
    }
}
