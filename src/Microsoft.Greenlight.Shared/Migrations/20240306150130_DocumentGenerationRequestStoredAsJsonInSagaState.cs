using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Microsoft.Greenlight.Shared.Migrations
{
    /// <inheritdoc />
    public partial class DocumentGenerationRequestStoredAsJsonInSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.DropColumn(
                name: "ProjectedProjectEndDate",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.DropColumn(
                name: "ProjectedProjectStartDate",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.DropColumn(
                name: "ReactorModel",
                table: "DocumentGenerationSagaStates");
            
            migrationBuilder.AddColumn<string>(
                name: "DocumentProcessName",
                table: "DocumentGenerationSagaStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DocumentGenerationRequest",
                table: "DocumentGenerationSagaStates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentGenerationRequest",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.DropColumn(
                name: "DocumentProcessName",
                table: "DocumentGenerationSagaStates");

            migrationBuilder.AddColumn<string>(
                name: "ReactorModel",
                table: "DocumentGenerationSagaStates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLatitude",
                table: "DocumentGenerationSagaStates",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLongitude",
                table: "DocumentGenerationSagaStates",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProjectedProjectEndDate",
                table: "DocumentGenerationSagaStates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProjectedProjectStartDate",
                table: "DocumentGenerationSagaStates",
                type: "date",
                nullable: true);
        }
    }
}
