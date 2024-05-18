using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenameDynamicDocumentProcessDefinitionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DynamicDocumentProcessDefinition",
                table: "DynamicDocumentProcessDefinition");

            migrationBuilder.RenameTable(
                name: "DynamicDocumentProcessDefinition",
                newName: "DynamicDocumentProcessDefinitions");

            migrationBuilder.RenameIndex(
                name: "IX_DynamicDocumentProcessDefinition_ShortName",
                table: "DynamicDocumentProcessDefinitions",
                newName: "IX_DynamicDocumentProcessDefinitions_ShortName");

            migrationBuilder.RenameIndex(
                name: "IX_DynamicDocumentProcessDefinition_LogicType",
                table: "DynamicDocumentProcessDefinitions",
                newName: "IX_DynamicDocumentProcessDefinitions_LogicType");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "DynamicDocumentProcessDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DynamicDocumentProcessDefinitions",
                table: "DynamicDocumentProcessDefinitions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_DeletedAt_IsActive",
                table: "DynamicDocumentProcessDefinitions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_IsActive",
                table: "DynamicDocumentProcessDefinitions",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DynamicDocumentProcessDefinitions",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_DeletedAt_IsActive",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_IsActive",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.RenameTable(
                name: "DynamicDocumentProcessDefinitions",
                newName: "DynamicDocumentProcessDefinition");

            migrationBuilder.RenameIndex(
                name: "IX_DynamicDocumentProcessDefinitions_ShortName",
                table: "DynamicDocumentProcessDefinition",
                newName: "IX_DynamicDocumentProcessDefinition_ShortName");

            migrationBuilder.RenameIndex(
                name: "IX_DynamicDocumentProcessDefinitions_LogicType",
                table: "DynamicDocumentProcessDefinition",
                newName: "IX_DynamicDocumentProcessDefinition_LogicType");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "DynamicDocumentProcessDefinition",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DynamicDocumentProcessDefinition",
                table: "DynamicDocumentProcessDefinition",
                column: "Id");
        }
    }
}
