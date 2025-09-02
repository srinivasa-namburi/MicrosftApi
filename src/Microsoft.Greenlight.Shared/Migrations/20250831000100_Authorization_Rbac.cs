using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class Authorization_Rbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auth_Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Auth_Permissions", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_Auth_Permissions_Key",
                table: "Auth_Permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateTable(
                name: "Auth_Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntraAppRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Auth_Roles", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_Auth_Roles_Name",
                table: "Auth_Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auth_Roles_EntraAppRoleId",
                table: "Auth_Roles",
                column: "EntraAppRoleId");

            migrationBuilder.CreateTable(
                name: "Auth_RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auth_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auth_RolePermissions_Auth_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Auth_Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Auth_RolePermissions_Auth_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Auth_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auth_RolePermissions_RoleId_PermissionId",
                table: "Auth_RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "Auth_UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderSubjectId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsFromEntra = table.Column<bool>(type: "bit", nullable: false),
                    AssignedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auth_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auth_UserRoles_Auth_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Auth_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auth_UserRoles_ProviderSubjectId_RoleId",
                table: "Auth_UserRoles",
                columns: new[] { "ProviderSubjectId", "RoleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Auth_UserRoles");
            migrationBuilder.DropTable(name: "Auth_RolePermissions");
            migrationBuilder.DropTable(name: "Auth_Permissions");
            migrationBuilder.DropTable(name: "Auth_Roles");
        }
    }
}
