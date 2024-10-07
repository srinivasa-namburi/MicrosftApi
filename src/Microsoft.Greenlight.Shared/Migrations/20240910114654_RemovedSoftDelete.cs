using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RemovedSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserInformations_DeletedAt_IsActive",
                table: "UserInformations");

            migrationBuilder.DropIndex(
                name: "IX_UserInformations_IsActive",
                table: "UserInformations");

            migrationBuilder.DropIndex(
                name: "IX_Tables_DeletedAt_IsActive",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_IsActive",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_TableCells_DeletedAt_IsActive",
                table: "TableCells");

            migrationBuilder.DropIndex(
                name: "IX_TableCells_IsActive",
                table: "TableCells");

            migrationBuilder.DropIndex(
                name: "IX_PromptVariableDefinitions_DeletedAt_IsActive",
                table: "PromptVariableDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PromptVariableDefinitions_IsActive",
                table: "PromptVariableDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PromptImplementations_DeletedAt_IsActive",
                table: "PromptImplementations");

            migrationBuilder.DropIndex(
                name: "IX_PromptImplementations_IsActive",
                table: "PromptImplementations");

            migrationBuilder.DropIndex(
                name: "IX_PromptDefinitions_DeletedAt_IsActive",
                table: "PromptDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PromptDefinitions_IsActive",
                table: "PromptDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_DeletedAt_IsActive",
                table: "IngestedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_IngestedDocuments_IsActive",
                table: "IngestedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedDocuments_DeletedAt_IsActive",
                table: "GeneratedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedDocuments_IsActive",
                table: "GeneratedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_DeletedAt_IsActive",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_IsActive",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentOutlines_DeletedAt_IsActive",
                table: "DocumentOutlines");

            migrationBuilder.DropIndex(
                name: "IX_DocumentOutlines_IsActive",
                table: "DocumentOutlines");

            migrationBuilder.DropIndex(
                name: "IX_DocumentMetadata_DeletedAt_IsActive",
                table: "DocumentMetadata");

            migrationBuilder.DropIndex(
                name: "IX_DocumentMetadata_IsActive",
                table: "DocumentMetadata");

            migrationBuilder.DropIndex(
                name: "IX_ConversationSummaries_DeletedAt_IsActive",
                table: "ConversationSummaries");

            migrationBuilder.DropIndex(
                name: "IX_ConversationSummaries_IsActive",
                table: "ConversationSummaries");

            migrationBuilder.DropIndex(
                name: "IX_ContentNodes_DeletedAt_IsActive",
                table: "ContentNodes");

            migrationBuilder.DropIndex(
                name: "IX_ContentNodes_IsActive",
                table: "ContentNodes");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_DeletedAt_IsActive",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_IsActive",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_DeletedAt_IsActive",
                table: "ChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_IsActive",
                table: "ChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_BoundingRegions_DeletedAt_IsActive",
                table: "BoundingRegions");

            migrationBuilder.DropIndex(
                name: "IX_BoundingRegions_IsActive",
                table: "BoundingRegions");

            migrationBuilder.DropIndex(
                name: "IX_BoundingPolygons_DeletedAt_IsActive",
                table: "BoundingPolygons");

            migrationBuilder.DropIndex(
                name: "IX_BoundingPolygons_IsActive",
                table: "BoundingPolygons");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserInformations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "UserInformations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TableCells");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TableCells");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PromptVariableDefinitions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PromptVariableDefinitions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PromptImplementations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PromptImplementations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PromptDefinitions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PromptDefinitions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DocumentOutlines");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DocumentOutlines");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ConversationSummaries");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ConversationSummaries");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BoundingRegions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BoundingRegions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BoundingPolygons");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BoundingPolygons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "UserInformations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "UserInformations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Tables",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tables",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "TableCells",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TableCells",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "PromptVariableDefinitions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PromptVariableDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "PromptImplementations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PromptImplementations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "PromptDefinitions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PromptDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "IngestedDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "IngestedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "GeneratedDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "GeneratedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "DynamicDocumentProcessDefinitions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DynamicDocumentProcessDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "DocumentOutlines",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DocumentOutlines",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "DocumentMetadata",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DocumentMetadata",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ConversationSummaries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ConversationSummaries",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ContentNodes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ContentNodes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ChatMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ChatConversations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ChatConversations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "BoundingRegions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BoundingRegions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "BoundingPolygons",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BoundingPolygons",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserInformations_DeletedAt_IsActive",
                table: "UserInformations",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInformations_IsActive",
                table: "UserInformations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_DeletedAt_IsActive",
                table: "Tables",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Tables_IsActive",
                table: "Tables",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TableCells_DeletedAt_IsActive",
                table: "TableCells",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TableCells_IsActive",
                table: "TableCells",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_DeletedAt_IsActive",
                table: "PromptVariableDefinitions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariableDefinitions_IsActive",
                table: "PromptVariableDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromptImplementations_DeletedAt_IsActive",
                table: "PromptImplementations",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptImplementations_IsActive",
                table: "PromptImplementations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromptDefinitions_DeletedAt_IsActive",
                table: "PromptDefinitions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptDefinitions_IsActive",
                table: "PromptDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_DeletedAt_IsActive",
                table: "IngestedDocuments",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_IngestedDocuments_IsActive",
                table: "IngestedDocuments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_DeletedAt_IsActive",
                table: "GeneratedDocuments",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_IsActive",
                table: "GeneratedDocuments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_DeletedAt_IsActive",
                table: "DynamicDocumentProcessDefinitions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_IsActive",
                table: "DynamicDocumentProcessDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlines_DeletedAt_IsActive",
                table: "DocumentOutlines",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlines_IsActive",
                table: "DocumentOutlines",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_DeletedAt_IsActive",
                table: "DocumentMetadata",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_IsActive",
                table: "DocumentMetadata",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_DeletedAt_IsActive",
                table: "ConversationSummaries",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_IsActive",
                table: "ConversationSummaries",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_DeletedAt_IsActive",
                table: "ContentNodes",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentNodes_IsActive",
                table: "ContentNodes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_DeletedAt_IsActive",
                table: "ChatMessages",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_IsActive",
                table: "ChatMessages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_DeletedAt_IsActive",
                table: "ChatConversations",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_IsActive",
                table: "ChatConversations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingRegions_DeletedAt_IsActive",
                table: "BoundingRegions",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BoundingRegions_IsActive",
                table: "BoundingRegions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BoundingPolygons_DeletedAt_IsActive",
                table: "BoundingPolygons",
                columns: new[] { "DeletedAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BoundingPolygons_IsActive",
                table: "BoundingPolygons",
                column: "IsActive");
        }
    }
}
