using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenamedReviewModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewQuestions_Reviews_ReviewId",
                table: "ReviewQuestions");

            migrationBuilder.DropTable(
                name: "ReviewDocumentProcessDefinitions");

            migrationBuilder.DropTable(
                name: "ReviewRequests");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.CreateTable(
                name: "ReviewDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewDefinitionDocumentProcessDefinition",
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
                    table.PrimaryKey("PK_ReviewDefinitionDocumentProcessDefinition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDefinitionDocumentProcessDefinition_DynamicDocumentProcessDefinitions_DocumentProcessDefinitionId",
                        column: x => x.DocumentProcessDefinitionId,
                        principalTable: "DynamicDocumentProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewDefinitionDocumentProcessDefinition_ReviewDefinitions_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "ReviewDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullBlobUrlToReviewedDocument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewInstances_ReviewDefinitions_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "ReviewDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDefinitionDocumentProcessDefinition_DocumentProcessDefinitionId",
                table: "ReviewDefinitionDocumentProcessDefinition",
                column: "DocumentProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDefinitionDocumentProcessDefinition_IsActive",
                table: "ReviewDefinitionDocumentProcessDefinition",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDefinitionDocumentProcessDefinition_ReviewId",
                table: "ReviewDefinitionDocumentProcessDefinition",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDefinitionDocumentProcessDefinition_ReviewId_DocumentProcessDefinitionId",
                table: "ReviewDefinitionDocumentProcessDefinition",
                columns: new[] { "ReviewId", "DocumentProcessDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewInstances_ReviewId",
                table: "ReviewInstances",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewInstances_Status",
                table: "ReviewInstances",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewQuestions_ReviewDefinitions_ReviewId",
                table: "ReviewQuestions",
                column: "ReviewId",
                principalTable: "ReviewDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewQuestions_ReviewDefinitions_ReviewId",
                table: "ReviewQuestions");

            migrationBuilder.DropTable(
                name: "ReviewDefinitionDocumentProcessDefinition");

            migrationBuilder.DropTable(
                name: "ReviewInstances");

            migrationBuilder.DropTable(
                name: "ReviewDefinitions");

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                    DocumentProcessDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                name: "ReviewRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullBlobUrlToReviewedDocument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewRequests_Reviews_ReviewId",
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
                name: "IX_ReviewRequests_ReviewId",
                table: "ReviewRequests",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_Status",
                table: "ReviewRequests",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewQuestions_Reviews_ReviewId",
                table: "ReviewQuestions",
                column: "ReviewId",
                principalTable: "Reviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
