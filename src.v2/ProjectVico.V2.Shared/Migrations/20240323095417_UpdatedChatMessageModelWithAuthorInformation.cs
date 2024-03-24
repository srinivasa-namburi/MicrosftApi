using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedChatMessageModelWithAuthorInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorUserInformationId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_AuthorUserInformationId",
                table: "ChatMessages",
                column: "AuthorUserInformationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_UserInformations_AuthorUserInformationId",
                table: "ChatMessages",
                column: "AuthorUserInformationId",
                principalTable: "UserInformations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_UserInformations_AuthorUserInformationId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_AuthorUserInformationId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "AuthorUserInformationId",
                table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
