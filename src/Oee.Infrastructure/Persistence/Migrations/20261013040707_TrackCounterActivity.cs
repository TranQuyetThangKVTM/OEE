using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackCounterActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastCounterChangedAt",
                schema: "plc",
                table: "CounterCheckpoints",
                type: "datetimeoffset",
                nullable: true);
            // Checkpoint cũ chưa lưu mốc đổi counter: lấy lần đọc đã ghi làm mốc khởi đầu.
            migrationBuilder.Sql("UPDATE [plc].[CounterCheckpoints] SET [LastCounterChangedAt] = [LastReadAt];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCounterChangedAt",
                schema: "plc",
                table: "CounterCheckpoints");
        }
    }
}
