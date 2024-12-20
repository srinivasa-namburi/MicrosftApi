using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddedSearchParametersToDocumentProcessesBackend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FollowingSearchPartitionInclusionCount",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "MinimumRelevanceForCitations",
                table: "DynamicDocumentProcessDefinitions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfCitationsToGetFromRepository",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrecedingSearchPartitionInclusionCount",
                table: "DynamicDocumentProcessDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowingSearchPartitionInclusionCount",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "MinimumRelevanceForCitations",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "NumberOfCitationsToGetFromRepository",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "PrecedingSearchPartitionInclusionCount",
                table: "DynamicDocumentProcessDefinitions");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
