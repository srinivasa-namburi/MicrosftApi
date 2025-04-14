using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ChangedDbConfigurationToUseGuidKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First backup any existing data
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Configurations')
                BEGIN
                    SELECT * 
                    INTO ConfigurationsBackup
                    FROM Configurations;
                END
            ");

            // Drop the existing table if it exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Configurations')
                BEGIN
                    DROP TABLE Configurations;
                END
            ");

            // Create the new table with Guid as primary key
            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationValues = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Id);
                });

            // Insert the default configuration record
            migrationBuilder.Sql($@"
                INSERT INTO Configurations (Id, ConfigurationValues, LastUpdated, LastUpdatedBy)
                VALUES ('52d7cb18-1543-4156-b535-8a7defbf9066', '{{}}', GETUTCDATE(), 'System');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new table
            migrationBuilder.DropTable(
                name: "Configurations");

            // Recreate the old table with int identity
            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigurationValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Id);
                });

            // Restore data if backup exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ConfigurationsBackup')
                BEGIN
                    INSERT INTO Configurations (ConfigurationValues, LastUpdated, LastUpdatedBy)
                    SELECT ConfigurationValues, LastUpdated, LastUpdatedBy
                    FROM ConfigurationsBackup;
                    
                    DROP TABLE ConfigurationsBackup;
                END
            ");
        }

    }
}
