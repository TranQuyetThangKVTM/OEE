using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionPlanVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "ProductionPlans",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "ProductionPlans",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesPlanId",
                table: "ProductionPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ProductionPlans",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ProductionPlans",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "VersionSeriesId",
                table: "ProductionPlans",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE [ProductionPlans] SET [VersionSeriesId] = [Id] WHERE [VersionSeriesId] = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_ReplacesPlanId",
                table: "ProductionPlans",
                column: "ReplacesPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_VersionSeriesId_Version",
                table: "ProductionPlans",
                columns: new[] { "VersionSeriesId", "Version" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionPlans_Version",
                table: "ProductionPlans",
                sql: "[Version] > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionPlans_ProductionPlans_ReplacesPlanId",
                table: "ProductionPlans",
                column: "ReplacesPlanId",
                principalTable: "ProductionPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionPlans_ProductionPlans_ReplacesPlanId",
                table: "ProductionPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProductionPlans_ReplacesPlanId",
                table: "ProductionPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProductionPlans_VersionSeriesId_Version",
                table: "ProductionPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionPlans_Version",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "ReplacesPlanId",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "VersionSeriesId",
                table: "ProductionPlans");
        }
    }
}
