using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionPlanSampleTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceSampleProductionPlanId",
                table: "ProductionPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceSampleProductionPlanItemId",
                table: "ProductionPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_SourceSampleProductionPlanId_SourceSampleProductionPlanItemId",
                table: "ProductionPlans",
                columns: new[] { "SourceSampleProductionPlanId", "SourceSampleProductionPlanItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_SourceSampleProductionPlanItemId",
                table: "ProductionPlans",
                column: "SourceSampleProductionPlanItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionPlans_SampleProductionPlanItems_SourceSampleProductionPlanItemId",
                table: "ProductionPlans",
                column: "SourceSampleProductionPlanItemId",
                principalTable: "SampleProductionPlanItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionPlans_SampleProductionPlans_SourceSampleProductionPlanId",
                table: "ProductionPlans",
                column: "SourceSampleProductionPlanId",
                principalTable: "SampleProductionPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionPlans_SampleProductionPlanItems_SourceSampleProductionPlanItemId",
                table: "ProductionPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionPlans_SampleProductionPlans_SourceSampleProductionPlanId",
                table: "ProductionPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProductionPlans_SourceSampleProductionPlanId_SourceSampleProductionPlanItemId",
                table: "ProductionPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProductionPlans_SourceSampleProductionPlanItemId",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "SourceSampleProductionPlanId",
                table: "ProductionPlans");

            migrationBuilder.DropColumn(
                name: "SourceSampleProductionPlanItemId",
                table: "ProductionPlans");
        }
    }
}
