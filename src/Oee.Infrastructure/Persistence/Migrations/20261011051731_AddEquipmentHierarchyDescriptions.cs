using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentHierarchyDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "org",
                table: "WorkUnits",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "org",
                table: "WorkCenters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "org",
                table: "Sites",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "org",
                table: "Areas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                schema: "org",
                table: "WorkUnits");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "org",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "org",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "org",
                table: "Areas");
        }
    }
}
