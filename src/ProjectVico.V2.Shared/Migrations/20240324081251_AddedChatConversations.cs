using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatConversation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentProcessName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversation", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversation_DeletedAt_IsActive",
                table: "ChatConversation",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversation_IsActive",
                table: "ChatConversation",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatConversation_ConversationId",
                table: "ChatMessages",
                column: "ConversationId",
                principalTable: "ChatConversation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatConversation_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatConversation");
        }
    }
}
