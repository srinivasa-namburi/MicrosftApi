using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicDocumentProcessDefinitionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DynamicDocumentProcessDefinition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Repositories = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogicType = table.Column<int>(type: "int", nullable: false),
                    BlobStorageContainerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobStorageAutoImportFolderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClassifyDocuments = table.Column<bool>(type: "bit", nullable: false),
                    ClassificationModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicDocumentProcessDefinition", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinition_LogicType",
                table: "DynamicDocumentProcessDefinition",
                column: "LogicType");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicDocumentProcessDefinition_ShortName",
                table: "DynamicDocumentProcessDefinition",
                column: "ShortName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DynamicDocumentProcessDefinition");
        }
    }
}
