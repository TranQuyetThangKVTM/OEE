using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrganizeOeeSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_WorkUnitStandards_PreventEffectiveOverlap]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_OeeTargets_PreventEffectiveOverlap]");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "plc");

            migrationBuilder.EnsureSchema(
                name: "downtime");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "org");

            migrationBuilder.EnsureSchema(
                name: "oee");

            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.EnsureSchema(
                name: "production");

            migrationBuilder.EnsureSchema(
                name: "planning");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.RenameTable(
                name: "WorkUnitStandards",
                newName: "WorkUnitStandards",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "WorkUnits",
                newName: "WorkUnits",
                newSchema: "org");

            migrationBuilder.RenameTable(
                name: "WorkUnitGroups",
                newName: "WorkUnitGroups",
                newSchema: "org");

            migrationBuilder.RenameTable(
                name: "UserTokens",
                newName: "UserTokens",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Users",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                newName: "UserRoles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "UserLogins",
                newName: "UserLogins",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "UserClaims",
                newName: "UserClaims",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Sites",
                newName: "Sites",
                newSchema: "org");

            migrationBuilder.RenameTable(
                name: "Shifts",
                newName: "Shifts",
                newSchema: "org");

            migrationBuilder.RenameTable(
                name: "SampleProductionPlans",
                newName: "SampleProductionPlans",
                newSchema: "planning");

            migrationBuilder.RenameTable(
                name: "SampleProductionPlanItems",
                newName: "SampleProductionPlanItems",
                newSchema: "planning");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "Roles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "RoleClaims",
                newName: "RoleClaims",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Products",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "ProductionReconciliationItems",
                newName: "ProductionReconciliationItems",
                newSchema: "production");

            migrationBuilder.RenameTable(
                name: "ProductionPlans",
                newName: "ProductionPlans",
                newSchema: "planning");

            migrationBuilder.RenameTable(
                name: "ProductionOutputs",
                newName: "ProductionOutputs",
                newSchema: "production");

            migrationBuilder.RenameTable(
                name: "Processes",
                newName: "Processes",
                newSchema: "org");

            migrationBuilder.RenameTable(
                name: "PlcSessionEvents",
                newName: "PlcSessionEvents",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "PlcIntegrationEvents",
                newName: "PlcIntegrationEvents",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "PlcFaultCodeMappings",
                newName: "PlcFaultCodeMappings",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "PlcDeviceRuntimeStates",
                newName: "PlcDeviceRuntimeStates",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "PlcDeviceMappings",
                newName: "PlcDeviceMappings",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                newName: "OutboxMessages",
                newSchema: "integration");

            migrationBuilder.RenameTable(
                name: "OpcUaTagMappings",
                newName: "OpcUaTagMappings",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "OpcUaConnections",
                newName: "OpcUaConnections",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "OeeTargets",
                newName: "OeeTargets",
                newSchema: "oee");

            migrationBuilder.RenameTable(
                name: "OeeSnapshots",
                newName: "OeeSnapshots",
                newSchema: "oee");

            migrationBuilder.RenameTable(
                name: "OeeAlerts",
                newName: "OeeAlerts",
                newSchema: "oee");

            migrationBuilder.RenameTable(
                name: "Enterprises",
                newName: "Enterprises",
                newSchema: "org");

            migrationBuilder.RenameTable(
                name: "DowntimeReasons",
                newName: "DowntimeReasons",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "DowntimeEvents",
                newName: "DowntimeEvents",
                newSchema: "downtime");

            migrationBuilder.RenameTable(
                name: "CounterCheckpoints",
                newName: "CounterCheckpoints",
                newSchema: "plc");

            migrationBuilder.RenameTable(
                name: "AuditRetentionRuns",
                newName: "AuditRetentionRuns",
                newSchema: "audit");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "AuditLogs",
                newSchema: "audit");

            CreateWorkUnitStandardsOverlapTrigger(migrationBuilder, "catalog");
            CreateOeeTargetsOverlapTrigger(migrationBuilder, "oee");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [catalog].[TR_WorkUnitStandards_PreventEffectiveOverlap]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [oee].[TR_OeeTargets_PreventEffectiveOverlap]");

            migrationBuilder.RenameTable(
                name: "WorkUnitStandards",
                schema: "catalog",
                newName: "WorkUnitStandards");

            migrationBuilder.RenameTable(
                name: "WorkUnits",
                schema: "org",
                newName: "WorkUnits");

            migrationBuilder.RenameTable(
                name: "WorkUnitGroups",
                schema: "org",
                newName: "WorkUnitGroups");

            migrationBuilder.RenameTable(
                name: "UserTokens",
                schema: "identity",
                newName: "UserTokens");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "identity",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                schema: "identity",
                newName: "UserRoles");

            migrationBuilder.RenameTable(
                name: "UserLogins",
                schema: "identity",
                newName: "UserLogins");

            migrationBuilder.RenameTable(
                name: "UserClaims",
                schema: "identity",
                newName: "UserClaims");

            migrationBuilder.RenameTable(
                name: "Sites",
                schema: "org",
                newName: "Sites");

            migrationBuilder.RenameTable(
                name: "Shifts",
                schema: "org",
                newName: "Shifts");

            migrationBuilder.RenameTable(
                name: "SampleProductionPlans",
                schema: "planning",
                newName: "SampleProductionPlans");

            migrationBuilder.RenameTable(
                name: "SampleProductionPlanItems",
                schema: "planning",
                newName: "SampleProductionPlanItems");

            migrationBuilder.RenameTable(
                name: "Roles",
                schema: "identity",
                newName: "Roles");

            migrationBuilder.RenameTable(
                name: "RoleClaims",
                schema: "identity",
                newName: "RoleClaims");

            migrationBuilder.RenameTable(
                name: "Products",
                schema: "catalog",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "ProductionReconciliationItems",
                schema: "production",
                newName: "ProductionReconciliationItems");

            migrationBuilder.RenameTable(
                name: "ProductionPlans",
                schema: "planning",
                newName: "ProductionPlans");

            migrationBuilder.RenameTable(
                name: "ProductionOutputs",
                schema: "production",
                newName: "ProductionOutputs");

            migrationBuilder.RenameTable(
                name: "Processes",
                schema: "org",
                newName: "Processes");

            migrationBuilder.RenameTable(
                name: "PlcSessionEvents",
                schema: "plc",
                newName: "PlcSessionEvents");

            migrationBuilder.RenameTable(
                name: "PlcIntegrationEvents",
                schema: "plc",
                newName: "PlcIntegrationEvents");

            migrationBuilder.RenameTable(
                name: "PlcFaultCodeMappings",
                schema: "plc",
                newName: "PlcFaultCodeMappings");

            migrationBuilder.RenameTable(
                name: "PlcDeviceRuntimeStates",
                schema: "plc",
                newName: "PlcDeviceRuntimeStates");

            migrationBuilder.RenameTable(
                name: "PlcDeviceMappings",
                schema: "plc",
                newName: "PlcDeviceMappings");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                schema: "integration",
                newName: "OutboxMessages");

            migrationBuilder.RenameTable(
                name: "OpcUaTagMappings",
                schema: "plc",
                newName: "OpcUaTagMappings");

            migrationBuilder.RenameTable(
                name: "OpcUaConnections",
                schema: "plc",
                newName: "OpcUaConnections");

            migrationBuilder.RenameTable(
                name: "OeeTargets",
                schema: "oee",
                newName: "OeeTargets");

            migrationBuilder.RenameTable(
                name: "OeeSnapshots",
                schema: "oee",
                newName: "OeeSnapshots");

            migrationBuilder.RenameTable(
                name: "OeeAlerts",
                schema: "oee",
                newName: "OeeAlerts");

            migrationBuilder.RenameTable(
                name: "Enterprises",
                schema: "org",
                newName: "Enterprises");

            migrationBuilder.RenameTable(
                name: "DowntimeReasons",
                schema: "catalog",
                newName: "DowntimeReasons");

            migrationBuilder.RenameTable(
                name: "DowntimeEvents",
                schema: "downtime",
                newName: "DowntimeEvents");

            migrationBuilder.RenameTable(
                name: "CounterCheckpoints",
                schema: "plc",
                newName: "CounterCheckpoints");

            migrationBuilder.RenameTable(
                name: "AuditRetentionRuns",
                schema: "audit",
                newName: "AuditRetentionRuns");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                schema: "audit",
                newName: "AuditLogs");

            CreateWorkUnitStandardsOverlapTrigger(migrationBuilder, "dbo");
            CreateOeeTargetsOverlapTrigger(migrationBuilder, "dbo");
        }

        private static void CreateWorkUnitStandardsOverlapTrigger(
            MigrationBuilder migrationBuilder,
            string schema)
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER [{schema}].[TR_WorkUnitStandards_PreventEffectiveOverlap]
                ON [{schema}].[WorkUnitStandards]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [{schema}].[WorkUnitStandards] e
                          ON e.[WorkUnitId] = i.[WorkUnitId]
                         AND e.[ProductId] = i.[ProductId]
                         AND e.[Id] <> i.[Id]
                         AND e.[IsDeleted] = 0
                         AND i.[IsDeleted] = 0
                         AND e.[EffectiveFrom] <= ISNULL(i.[EffectiveTo], CONVERT(date, '99991231', 112))
                         AND i.[EffectiveFrom] <= ISNULL(e.[EffectiveTo], CONVERT(date, '99991231', 112)))
                    BEGIN
                        THROW 51001, 'WorkUnitStandard effective periods cannot overlap within the same WorkUnit/Product scope.', 1;
                    END
                END
                """);
        }

        private static void CreateOeeTargetsOverlapTrigger(
            MigrationBuilder migrationBuilder,
            string schema)
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER [{schema}].[TR_OeeTargets_PreventEffectiveOverlap]
                ON [{schema}].[OeeTargets]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [{schema}].[OeeTargets] e
                          ON ((e.[WorkUnitId] = i.[WorkUnitId]) OR (e.[WorkUnitId] IS NULL AND i.[WorkUnitId] IS NULL))
                         AND ((e.[SiteId] = i.[SiteId]) OR (e.[SiteId] IS NULL AND i.[SiteId] IS NULL))
                         AND e.[PeriodType] = i.[PeriodType]
                         AND e.[PeriodCode] = i.[PeriodCode]
                         AND e.[Id] <> i.[Id]
                         AND e.[IsDeleted] = 0
                         AND i.[IsDeleted] = 0
                         AND e.[EffectiveFrom] <= ISNULL(i.[EffectiveTo], CONVERT(date, '99991231', 112))
                         AND i.[EffectiveFrom] <= ISNULL(e.[EffectiveTo], CONVERT(date, '99991231', 112)))
                    BEGIN
                        THROW 51002, 'OeeTarget effective periods cannot overlap within the same scope and period.', 1;
                    END
                END
                """);
        }
    }
}
