using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMcpPluginVersionArguments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First backup existing data so we don't lose it
            migrationBuilder.Sql(@"
                UPDATE McpPluginVersions
                SET Arguments = CASE 
                    WHEN Arguments IS NULL THEN ''
                    WHEN Arguments = '' THEN ''
                    ELSE Arguments
                END
            ");
            
            // Now rename the column to keep the data
            migrationBuilder.RenameColumn(
                name: "Arguments",
                table: "McpPluginVersions",
                newName: "ArgumentsString");
            
            // Add the new column with the proper conversion format
            migrationBuilder.AddColumn<string>(
                name: "Arguments",
                table: "McpPluginVersions",
                type: "nvarchar(max)",
                nullable: true);
            
            // Convert existing data to the new format
            migrationBuilder.Sql(@"
                UPDATE McpPluginVersions
                SET Arguments = ArgumentsString
            ");
            
            // Drop the backup column
            migrationBuilder.DropColumn(
                name: "ArgumentsString",
                table: "McpPluginVersions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No need to convert back since the storage format is compatible
        }
    }
}