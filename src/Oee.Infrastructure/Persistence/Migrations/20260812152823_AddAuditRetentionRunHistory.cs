using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRetentionRunHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRetentionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CutoffUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetentionMonths = table.Column<int>(type: "int", nullable: false),
                    CandidateCount = table.Column<int>(type: "int", nullable: false),
                    DeletedCount = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRetentionRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRetentionRuns_CutoffUtc",
                table: "AuditRetentionRuns",
                column: "CutoffUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRetentionRuns_StartedAtUtc",
                table: "AuditRetentionRuns",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRetentionRuns");
        }
    }
}
