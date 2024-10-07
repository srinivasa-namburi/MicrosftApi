using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ConnectedChatMessagesAndConversationSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SummaryOfMessagesFromPriorSummary",
                table: "ConversationSummaries");

            migrationBuilder.AddColumn<string>(
                name: "SummaryText",
                table: "ConversationSummaries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SummarizedByConversationSummaryId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_CreatedAt",
                table: "ChatMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SummarizedByConversationSummaryId",
                table: "ChatMessages",
                column: "SummarizedByConversationSummaryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ConversationSummaries_SummarizedByConversationSummaryId",
                table: "ChatMessages",
                column: "SummarizedByConversationSummaryId",
                principalTable: "ConversationSummaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ConversationSummaries_SummarizedByConversationSummaryId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_CreatedAt",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SummarizedByConversationSummaryId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SummaryText",
                table: "ConversationSummaries");

            migrationBuilder.DropColumn(
                name: "SummarizedByConversationSummaryId",
                table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "SummaryOfMessagesFromPriorSummary",
                table: "ConversationSummaries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
