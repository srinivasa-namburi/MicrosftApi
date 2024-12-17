using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class CreatedAtAndModfiedAtPropertiesOnEntityBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "UserInformations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "UserInformations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "Tables",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "Tables",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "TableCells",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "TableCells",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "SourceReferenceItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "SourceReferenceItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ReviewQuestions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ReviewQuestions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ReviewQuestionAnswers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ReviewQuestionAnswers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ReviewInstances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ReviewInstances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ReviewDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ReviewDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ReviewDefinitionDocumentProcessDefinition",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ReviewDefinitionDocumentProcessDefinition",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "PromptVariableDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "PromptVariableDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "PromptImplementations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "PromptImplementations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "PromptDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "PromptDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "IngestedDocuments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "IngestedDocuments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "GeneratedDocuments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "GeneratedDocuments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ExportedDocumentLinks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ExportedDocumentLinks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DynamicPlugins",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DynamicPlugins",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DynamicPluginDocumentProcesses",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DynamicPluginDocumentProcesses",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DynamicDocumentProcessDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DynamicDocumentProcessDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DocumentOutlines",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DocumentOutlines",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DocumentOutlineItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DocumentOutlineItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DocumentMetadata",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DocumentMetadata",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DocumentLibraryDocumentProcessAssociations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DocumentLibraryDocumentProcessAssociations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "DocumentLibraries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "DocumentLibraries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ConversationSummaries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ConversationSummaries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ContentNodeSystemItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ContentNodeSystemItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ContentNodes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ContentNodes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ChatMessages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ChatMessages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "ChatConversations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "ChatConversations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "BoundingRegions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "BoundingRegions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "BoundingPolygons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "BoundingPolygons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "UserInformations");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "UserInformations");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "TableCells");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "TableCells");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "SourceReferenceItems");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ReviewQuestions");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ReviewQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ReviewQuestionAnswers");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ReviewQuestionAnswers");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ReviewInstances");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ReviewInstances");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ReviewDefinitions");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ReviewDefinitions");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ReviewDefinitionDocumentProcessDefinition");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ReviewDefinitionDocumentProcessDefinition");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "PromptVariableDefinitions");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "PromptVariableDefinitions");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "PromptImplementations");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "PromptImplementations");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "PromptDefinitions");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "PromptDefinitions");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "IngestedDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ExportedDocumentLinks");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ExportedDocumentLinks");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DynamicPlugins");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DynamicPlugins");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DynamicPluginDocumentProcesses");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DynamicPluginDocumentProcesses");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DocumentOutlines");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DocumentOutlines");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DocumentOutlineItems");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DocumentOutlineItems");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DocumentLibraryDocumentProcessAssociations");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DocumentLibraryDocumentProcessAssociations");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "DocumentLibraries");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "DocumentLibraries");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ConversationSummaries");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ConversationSummaries");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ContentNodeSystemItems");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ContentNodeSystemItems");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ContentNodes");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "BoundingRegions");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "BoundingRegions");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "BoundingPolygons");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "BoundingPolygons");
        }
    }
}
