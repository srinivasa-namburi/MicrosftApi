using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ContentEmbeddingsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentReferenceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChunkText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmbeddingVector = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    GeneratedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentEmbeddings_ContentReferenceItems_ContentReferenceItemId",
                        column: x => x.ContentReferenceItemId,
                        principalTable: "ContentReferenceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentEmbeddings_ContentReferenceItemId",
                table: "ContentEmbeddings",
                column: "ContentReferenceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentEmbeddings");
        }
    }
}
