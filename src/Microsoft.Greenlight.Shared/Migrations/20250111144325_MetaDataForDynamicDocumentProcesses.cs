using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MetaDataForDynamicDocumentProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DynamicDocumentProcessMetaDataFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DynamicDocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionToolTip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasPossibleValues = table.Column<bool>(type: "bit", nullable: false),
                    PossibleValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultPossibleValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicDocumentProcessMetaDataFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicDocumentProcessMetaDataFields_DynamicDocumentProcessDefinitions_DynamicDocumentProcessDefinitionId",
                        column: x => x.DynamicDocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessMetaDataFields_DynamicDocumentProcessDefinitionId",
                table: "DynamicDocumentProcessMetaDataFields",
                column: "DynamicDocumentProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessMetaDataFields_DynamicDocumentProcessDefinitionId_Name",
                table: "DynamicDocumentProcessMetaDataFields",
                columns: new[] { "DynamicDocumentProcessDefinitionId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DynamicDocumentProcessMetaDataFields");
        }
    }
}
