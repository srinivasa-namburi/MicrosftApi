using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenamedChatConversationTableToChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatConversation_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatConversation",
                table: "ChatConversation");

            migrationBuilder.RenameTable(
                name: "ChatConversation",
                newName: "ChatConversations");

            migrationBuilder.RenameIndex(
                name: "IX_ChatConversation_IsActive",
                table: "ChatConversations",
                newName: "IX_ChatConversations_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ChatConversation_DeletedAt_IsActive",
                table: "ChatConversations",
                newName: "IX_ChatConversations_DeletedAt_IsActive");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatConversations",
                table: "ChatConversations",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatConversations_ConversationId",
                table: "ChatMessages",
                column: "ConversationId",
                principalTable: "ChatConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatConversations_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatConversations",
                table: "ChatConversations");

            migrationBuilder.RenameTable(
                name: "ChatConversations",
                newName: "ChatConversation");

            migrationBuilder.RenameIndex(
                name: "IX_ChatConversations_IsActive",
                table: "ChatConversation",
                newName: "IX_ChatConversation_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ChatConversations_DeletedAt_IsActive",
                table: "ChatConversation",
                newName: "IX_ChatConversation_DeletedAt_IsActive");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatConversation",
                table: "ChatConversation",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatConversation_ConversationId",
                table: "ChatMessages",
                column: "ConversationId",
                principalTable: "ChatConversation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
