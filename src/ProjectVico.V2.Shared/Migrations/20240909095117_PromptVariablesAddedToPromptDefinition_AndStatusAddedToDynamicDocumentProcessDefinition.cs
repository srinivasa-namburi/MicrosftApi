using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class PromptVariablesAddedToPromptDefinition_AndStatusAddedToDynamicDocumentProcessDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PromptVariableDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromptDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariableName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptVariableDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptVariableDefinitions_PromptDefinitions_PromptDefinitionId",
                        column: x => x.PromptDefinitionId,
                        principalTable: "PromptDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_DeletedAt_IsActive",
                table: "PromptVariableDefinitions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_IsActive",
                table: "PromptVariableDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_PromptDefinitionId",
                table: "PromptVariableDefinitions",
                column: "PromptDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_VariableName",
                table: "PromptVariableDefinitions",
                column: "VariableName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptVariableDefinitions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DynamicDocumentProcessDefinitions");
        }
    }
}
