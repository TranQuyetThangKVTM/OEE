using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <summary>Đổi hierarchy bằng rename, giữ nguyên ID, RowVersion và dữ liệu lịch sử.</summary>
    public partial class RenameEquipmentHierarchyToAreaWorkCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Processes", schema: "org", newName: "Areas", newSchema: "org");
            migrationBuilder.RenameTable(name: "WorkUnitGroups", schema: "org", newName: "WorkCenters", newSchema: "org");
            migrationBuilder.RenameColumn(name: "ProcessId", schema: "org", table: "WorkCenters", newName: "AreaId");
            migrationBuilder.RenameColumn(name: "WorkUnitGroupId", schema: "org", table: "WorkUnits", newName: "WorkCenterId");
            migrationBuilder.RenameIndex(name: "IX_Processes_SiteId_Code", schema: "org", table: "Areas", newName: "IX_Areas_SiteId_Code");
            migrationBuilder.RenameIndex(name: "IX_WorkUnitGroups_ProcessId_Code", schema: "org", table: "WorkCenters", newName: "IX_WorkCenters_AreaId_Code");
            migrationBuilder.RenameIndex(name: "IX_WorkUnits_WorkUnitGroupId_Code", schema: "org", table: "WorkUnits", newName: "IX_WorkUnits_WorkCenterId_Code");

            // Đổi tên constraint tại chỗ, giữ nguyên dữ liệu và quan hệ được bảo vệ.
            migrationBuilder.Sql("""
                EXEC sp_rename N'[org].[PK_Processes]', N'PK_Areas', N'OBJECT';
                EXEC sp_rename N'[org].[PK_WorkUnitGroups]', N'PK_WorkCenters', N'OBJECT';
                EXEC sp_rename N'[org].[FK_Processes_Sites_SiteId]', N'FK_Areas_Sites_SiteId', N'OBJECT';
                EXEC sp_rename N'[org].[FK_WorkUnitGroups_Processes_ProcessId]', N'FK_WorkCenters_Areas_AreaId', N'OBJECT';
                EXEC sp_rename N'[org].[FK_WorkUnits_WorkUnitGroups_WorkUnitGroupId]', N'FK_WorkUnits_WorkCenters_WorkCenterId', N'OBJECT';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(name: "IX_Areas_SiteId_Code", schema: "org", table: "Areas", newName: "IX_Processes_SiteId_Code");
            migrationBuilder.RenameIndex(name: "IX_WorkCenters_AreaId_Code", schema: "org", table: "WorkCenters", newName: "IX_WorkUnitGroups_ProcessId_Code");
            migrationBuilder.RenameIndex(name: "IX_WorkUnits_WorkCenterId_Code", schema: "org", table: "WorkUnits", newName: "IX_WorkUnits_WorkUnitGroupId_Code");
            migrationBuilder.RenameColumn(name: "AreaId", schema: "org", table: "WorkCenters", newName: "ProcessId");
            migrationBuilder.RenameColumn(name: "WorkCenterId", schema: "org", table: "WorkUnits", newName: "WorkUnitGroupId");
            migrationBuilder.RenameTable(name: "Areas", schema: "org", newName: "Processes", newSchema: "org");
            migrationBuilder.RenameTable(name: "WorkCenters", schema: "org", newName: "WorkUnitGroups", newSchema: "org");

            // Đổi tên constraint tại chỗ, giữ nguyên dữ liệu và quan hệ được bảo vệ.
            migrationBuilder.Sql("""
                EXEC sp_rename N'[org].[PK_Areas]', N'PK_Processes', N'OBJECT';
                EXEC sp_rename N'[org].[PK_WorkCenters]', N'PK_WorkUnitGroups', N'OBJECT';
                EXEC sp_rename N'[org].[FK_Areas_Sites_SiteId]', N'FK_Processes_Sites_SiteId', N'OBJECT';
                EXEC sp_rename N'[org].[FK_WorkCenters_Areas_AreaId]', N'FK_WorkUnitGroups_Processes_ProcessId', N'OBJECT';
                EXEC sp_rename N'[org].[FK_WorkUnits_WorkCenters_WorkCenterId]', N'FK_WorkUnits_WorkUnitGroups_WorkUnitGroupId', N'OBJECT';
                """);
        }
    }
}
