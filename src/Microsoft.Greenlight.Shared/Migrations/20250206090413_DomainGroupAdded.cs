using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DomainGroupAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OriginalDocumentUrl",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentTitle",
                table: "DocumentGenerationSagaStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "DomainGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExposeCoPilotAgentEndpoint = table.Column<bool>(type: "bit", nullable: false),
                    AuthenticateCoPilotAgentEndpoint = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainGroupDynamicDocumentProcessDefinition",
                columns: table => new
                {
                    DocumentProcessesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DomainGroupMembershipsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainGroupDynamicDocumentProcessDefinition", x => new { x.DocumentProcessesId, x.DomainGroupMembershipsId });
                    table.ForeignKey(
                        name: "FK_DomainGroupDynamicDocumentProcessDefinition_DomainGroups_DomainGroupMembershipsId",
                        column: x => x.DomainGroupMembershipsId,
                        principalTable: "DomainGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DomainGroupDynamicDocumentProcessDefinition_DynamicDocumentProcessDefinitions_DocumentProcessesId",
                        column: x => x.DocumentProcessesId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainGroupDynamicDocumentProcessDefinition_DomainGroupMembershipsId",
                table: "DomainGroupDynamicDocumentProcessDefinition",
                column: "DomainGroupMembershipsId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainGroups_Name",
                table: "DomainGroups",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainGroupDynamicDocumentProcessDefinition");

            migrationBuilder.DropTable(
                name: "DomainGroups");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalDocumentUrl",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DocumentIngestionSagaStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentTitle",
                table: "DocumentGenerationSagaStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
