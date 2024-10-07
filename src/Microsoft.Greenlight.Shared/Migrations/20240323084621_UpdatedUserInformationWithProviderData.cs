using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedUserInformationWithProviderData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserInformation",
                table: "UserInformation");

            migrationBuilder.RenameTable(
                name: "UserInformation",
                newName: "UserInformations");

            migrationBuilder.RenameIndex(
                name: "IX_UserInformation_IsActive",
                table: "UserInformations",
                newName: "IX_UserInformations_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_UserInformation_DeletedAt_IsActive",
                table: "UserInformations",
                newName: "IX_UserInformations_DeletedAt_IsActive");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "UserInformations",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "UserInformations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSubjectId",
                table: "UserInformations",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserInformations",
                table: "UserInformations",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserInformations_Email",
                table: "UserInformations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_UserInformations_ProviderSubjectId",
                table: "UserInformations",
                column: "ProviderSubjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserInformations",
                table: "UserInformations");

            migrationBuilder.DropIndex(
                name: "IX_UserInformations_Email",
                table: "UserInformations");

            migrationBuilder.DropIndex(
                name: "IX_UserInformations_ProviderSubjectId",
                table: "UserInformations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "UserInformations");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "UserInformations");

            migrationBuilder.DropColumn(
                name: "ProviderSubjectId",
                table: "UserInformations");

            migrationBuilder.RenameTable(
                name: "UserInformations",
                newName: "UserInformation");

            migrationBuilder.RenameIndex(
                name: "IX_UserInformations_IsActive",
                table: "UserInformation",
                newName: "IX_UserInformation_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_UserInformations_DeletedAt_IsActive",
                table: "UserInformation",
                newName: "IX_UserInformation_DeletedAt_IsActive");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserInformation",
                table: "UserInformation",
                column: "Id");
        }
    }
}
