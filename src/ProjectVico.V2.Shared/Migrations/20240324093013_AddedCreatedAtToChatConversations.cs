using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedCreatedAtToChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatConversations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ChatConversations");
        }
    }
}
