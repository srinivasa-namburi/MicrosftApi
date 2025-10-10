using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddFlowTaskEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExposeToFlow",
                table: "McpPlugins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FlowTaskTemplateId",
                table: "DynamicDocumentProcessDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FlowTaskTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TriggerPhrases = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitialPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletionMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTaskTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowTaskDataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CacheDurationSeconds = table.Column<int>(type: "int", nullable: false),
                    FlowTaskTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    McpPluginId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransformPrompt = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTaskDataSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowTaskDataSources_FlowTaskTemplates_FlowTaskTemplateId",
                        column: x => x.FlowTaskTemplateId,
                        principalTable: "FlowTaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlowTaskDataSources_McpPlugins_McpPluginId",
                        column: x => x.McpPluginId,
                        principalTable: "McpPlugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowTaskOutputTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowTaskTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OutputType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    McpPluginId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    McpToolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExecutionOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    TransformationRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTaskOutputTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowTaskOutputTemplates_FlowTaskTemplates_FlowTaskTemplateId",
                        column: x => x.FlowTaskTemplateId,
                        principalTable: "FlowTaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowTaskSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowTaskTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SectionPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTaskSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowTaskSections_FlowTaskTemplates_FlowTaskTemplateId",
                        column: x => x.FlowTaskTemplateId,
                        principalTable: "FlowTaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowTaskMcpToolParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowTaskDataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParameterValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTaskMcpToolParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowTaskMcpToolParameters_FlowTaskDataSources_FlowTaskDataSourceId",
                        column: x => x.FlowTaskDataSourceId,
                        principalTable: "FlowTaskDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlowTaskRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowTaskSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "text"),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDataSourced = table.Column<bool>(type: "bit", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidationRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidOptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ConditionalLogicJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowTaskRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowTaskRequirements_FlowTaskDataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "FlowTaskDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlowTaskRequirements_FlowTaskSections_FlowTaskSectionId",
                        column: x => x.FlowTaskSectionId,
                        principalTable: "FlowTaskSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinitions_FlowTaskTemplateId",
                table: "DynamicDocumentProcessDefinitions",
                column: "FlowTaskTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskDataSources_FlowTaskTemplateId",
                table: "FlowTaskDataSources",
                column: "FlowTaskTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskDataSources_IsActive",
                table: "FlowTaskDataSources",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskDataSources_McpPluginId",
                table: "FlowTaskDataSources",
                column: "McpPluginId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskDataSources_Name",
                table: "FlowTaskDataSources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskDataSources_SourceType",
                table: "FlowTaskDataSources",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskMcpToolParameters_FlowTaskDataSourceId",
                table: "FlowTaskMcpToolParameters",
                column: "FlowTaskDataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskMcpToolParameters_FlowTaskDataSourceId_ParameterName",
                table: "FlowTaskMcpToolParameters",
                columns: new[] { "FlowTaskDataSourceId", "ParameterName" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskOutputTemplates_FlowTaskTemplateId",
                table: "FlowTaskOutputTemplates",
                column: "FlowTaskTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskOutputTemplates_FlowTaskTemplateId_Name",
                table: "FlowTaskOutputTemplates",
                columns: new[] { "FlowTaskTemplateId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskRequirements_DataSourceId",
                table: "FlowTaskRequirements",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskRequirements_FlowTaskSectionId",
                table: "FlowTaskRequirements",
                column: "FlowTaskSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskRequirements_FlowTaskSectionId_FieldName",
                table: "FlowTaskRequirements",
                columns: new[] { "FlowTaskSectionId", "FieldName" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskSections_FlowTaskTemplateId",
                table: "FlowTaskSections",
                column: "FlowTaskTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskSections_FlowTaskTemplateId_SortOrder",
                table: "FlowTaskSections",
                columns: new[] { "FlowTaskTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskTemplates_IsActive",
                table: "FlowTaskTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FlowTaskTemplates_Name",
                table: "FlowTaskTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_FlowTaskTemplates_FlowTaskTemplateId",
                table: "DynamicDocumentProcessDefinitions",
                column: "FlowTaskTemplateId",
                principalTable: "FlowTaskTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DynamicDocumentProcessDefinitions_FlowTaskTemplates_FlowTaskTemplateId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropTable(
                name: "FlowTaskMcpToolParameters");

            migrationBuilder.DropTable(
                name: "FlowTaskOutputTemplates");

            migrationBuilder.DropTable(
                name: "FlowTaskRequirements");

            migrationBuilder.DropTable(
                name: "FlowTaskDataSources");

            migrationBuilder.DropTable(
                name: "FlowTaskSections");

            migrationBuilder.DropTable(
                name: "FlowTaskTemplates");

            migrationBuilder.DropIndex(
                name: "IX_DynamicDocumentProcessDefinitions_FlowTaskTemplateId",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ExposeToFlow",
                table: "McpPlugins");

            migrationBuilder.DropColumn(
                name: "FlowTaskTemplateId",
                table: "DynamicDocumentProcessDefinitions");
        }
    }
}
