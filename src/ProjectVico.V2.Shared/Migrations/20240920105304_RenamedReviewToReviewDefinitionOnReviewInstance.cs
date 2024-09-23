using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVico.V2.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenamedReviewToReviewDefinitionOnReviewInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewInstances_ReviewDefinitions_ReviewId",
                table: "ReviewInstances");

            migrationBuilder.RenameColumn(
                name: "ReviewId",
                table: "ReviewInstances",
                newName: "ReviewDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_ReviewInstances_ReviewId",
                table: "ReviewInstances",
                newName: "IX_ReviewInstances_ReviewDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewInstances_ReviewDefinitions_ReviewDefinitionId",
                table: "ReviewInstances",
                column: "ReviewDefinitionId",
                principalTable: "ReviewDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewInstances_ReviewDefinitions_ReviewDefinitionId",
                table: "ReviewInstances");

            migrationBuilder.RenameColumn(
                name: "ReviewDefinitionId",
                table: "ReviewInstances",
                newName: "ReviewId");

            migrationBuilder.RenameIndex(
                name: "IX_ReviewInstances_ReviewDefinitionId",
                table: "ReviewInstances",
                newName: "IX_ReviewInstances_ReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewInstances_ReviewDefinitions_ReviewId",
                table: "ReviewInstances",
                column: "ReviewId",
                principalTable: "ReviewDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
