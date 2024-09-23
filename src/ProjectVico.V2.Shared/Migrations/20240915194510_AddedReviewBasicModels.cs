using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedReviewBasicModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewDocumentProcessDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDocumentProcessDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDocumentProcessDefinitions_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                        column: x => x.DocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewDocumentProcessDefinitions_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewQuestions_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDocumentProcessDefinitions_DocumentProcessDefinitionId",
                table: "ReviewDocumentProcessDefinitions",
                column: "DocumentProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDocumentProcessDefinitions_IsActive",
                table: "ReviewDocumentProcessDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDocumentProcessDefinitions_ReviewId",
                table: "ReviewDocumentProcessDefinitions",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDocumentProcessDefinitions_ReviewId_DocumentProcessDefinitionId",
                table: "ReviewDocumentProcessDefinitions",
                columns: new[] { "ReviewId", "DocumentProcessDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewQuestions_ReviewId",
                table: "ReviewQuestions",
                column: "ReviewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewDocumentProcessDefinitions");

            migrationBuilder.DropTable(
                name: "ReviewQuestions");

            migrationBuilder.DropTable(
                name: "Reviews");
        }
    }
}
