using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpcUaResilience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingOperationalState",
                table: "PlcDeviceRuntimeStates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingSampleCount",
                table: "PlcDeviceRuntimeStates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DebounceSamples",
                table: "PlcDeviceMappings",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "ZeroFaultCodeMeansNoFault",
                table: "PlcDeviceMappings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "OutboxMessages",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "OutboxMessages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthenticationType",
                table: "OpcUaConnections",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Anonymous");

            migrationBuilder.AddColumn<string>(
                name: "CertificateReference",
                table: "OpcUaConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialSecretReference",
                table: "OpcUaConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "OpcUaConnections",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSampleKey",
                table: "CounterCheckpoints",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("UPDATE [OutboxMessages] SET [IdempotencyKey] = CONVERT(nvarchar(36), [Id]), [ExpiresAt] = DATEADD(hour, 72, [OccurredAt]) WHERE [IdempotencyKey] = ''; ");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlcDeviceMappings_DebounceSamples",
                table: "PlcDeviceMappings",
                sql: "[DebounceSamples] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_IdempotencyKey",
                table: "OutboxMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_ExpiresAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlcDeviceMappings_DebounceSamples",
                table: "PlcDeviceMappings");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_IdempotencyKey",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_ExpiresAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "PendingOperationalState",
                table: "PlcDeviceRuntimeStates");

            migrationBuilder.DropColumn(
                name: "PendingSampleCount",
                table: "PlcDeviceRuntimeStates");

            migrationBuilder.DropColumn(
                name: "DebounceSamples",
                table: "PlcDeviceMappings");

            migrationBuilder.DropColumn(
                name: "ZeroFaultCodeMeansNoFault",
                table: "PlcDeviceMappings");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "AuthenticationType",
                table: "OpcUaConnections");

            migrationBuilder.DropColumn(
                name: "CertificateReference",
                table: "OpcUaConnections");

            migrationBuilder.DropColumn(
                name: "CredentialSecretReference",
                table: "OpcUaConnections");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "OpcUaConnections");

            migrationBuilder.DropColumn(
                name: "LastSampleKey",
                table: "CounterCheckpoints");
        }
    }
}
