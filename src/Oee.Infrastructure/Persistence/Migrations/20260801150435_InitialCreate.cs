using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EntitySubject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_ChangesJson", "[Changes] IS NULL OR ISJSON([Changes]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "DowntimeReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DowntimeReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enterprises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enterprises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpcUaConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EndpointUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SecurityMode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SecurityPolicy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcUaConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessages_PayloadJson", "ISJSON([Payload]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultUom = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SampleProductionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleProductionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnterpriseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sites_Enterprises_EnterpriseId",
                        column: x => x.EnterpriseId,
                        principalTable: "Enterprises",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlcSessionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpcUaConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlcSessionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlcSessionEvents_OpcUaConnections_OpcUaConnectionId",
                        column: x => x.OpcUaConnectionId,
                        principalTable: "OpcUaConnections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.CheckConstraint("CK_Shifts_DifferentTimes", "[StartTime] <> [EndTime]");
                    table.ForeignKey(
                        name: "FK_Shifts_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkUnitGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkUnitGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkUnitGroups_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperationalState = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkUnits_WorkUnitGroups_WorkUnitGroupId",
                        column: x => x.WorkUnitGroupId,
                        principalTable: "WorkUnitGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OeeSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    PeriodCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Availability = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Performance = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Quality = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Oee = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    TotalMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnscheduledMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LoadingMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AvailabilityLossMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UtilizationMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PerformanceLossMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetUtilizationMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QualityLossMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ValueAddedMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DefectQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GoodQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CalculationVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AnomalyFlags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OeeSnapshots", x => x.Id);
                    table.CheckConstraint("CK_OeeSnapshots_Period", "[PeriodEnd] > [PeriodStart]");
                    table.CheckConstraint("CK_OeeSnapshots_Ratios", "[Availability] BETWEEN 0 AND 1 AND [Performance] BETWEEN 0 AND 1 AND [Quality] BETWEEN 0 AND 1 AND [Oee] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_OeeSnapshots_Values", "[TotalMinutes] >= 0 AND [UnscheduledMinutes] >= 0 AND [TotalQuantity] >= 0 AND [DefectQuantity] >= 0 AND [GoodQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_OeeSnapshots_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OeeTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    PeriodCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MinimumOee = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    MinimumAvailability = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    MinimumPerformance = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    MinimumQuality = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OeeTargets", x => x.Id);
                    table.CheckConstraint("CK_OeeTargets_EffectiveRange", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_OeeTargets_Ratios", "[MinimumOee] BETWEEN 0 AND 1 AND [MinimumAvailability] BETWEEN 0 AND 1 AND [MinimumPerformance] BETWEEN 0 AND 1 AND [MinimumQuality] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_OeeTargets_Scope", "NOT ([WorkUnitId] IS NOT NULL AND [SiteId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_OeeTargets_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OeeTargets_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlcDeviceMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpcUaConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalDeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CounterMode = table.Column<int>(type: "int", nullable: false),
                    FaultCodeFormat = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlcDeviceMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlcDeviceMappings_OpcUaConnections_OpcUaConnectionId",
                        column: x => x.OpcUaConnectionId,
                        principalTable: "OpcUaConnections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlcDeviceMappings_Products_DefaultProductId",
                        column: x => x.DefaultProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlcDeviceMappings_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlcFaultCodeMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FaultCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DowntimeReasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlcFaultCodeMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlcFaultCodeMappings_DowntimeReasons_DowntimeReasonId",
                        column: x => x.DowntimeReasonId,
                        principalTable: "DowntimeReasons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlcFaultCodeMappings_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PlannedStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlannedEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPlans", x => x.Id);
                    table.CheckConstraint("CK_ProductionPlans_Period", "[PlannedEnd] > [PlannedStart]");
                    table.CheckConstraint("CK_ProductionPlans_Quantity", "[PlannedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductionPlans_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionPlans_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionPlans_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SampleProductionPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SampleProductionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleProductionPlanItems", x => x.Id);
                    table.CheckConstraint("CK_SampleProductionPlanItems_Quantity", "[PlannedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_SampleProductionPlanItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SampleProductionPlanItems_SampleProductionPlans_SampleProductionPlanId",
                        column: x => x.SampleProductionPlanId,
                        principalTable: "SampleProductionPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SampleProductionPlanItems_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SampleProductionPlanItems_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkUnitStandards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StandardSpeedPerMinute = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Uom = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkUnitStandards", x => x.Id);
                    table.CheckConstraint("CK_WorkUnitStandards_EffectiveRange", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_WorkUnitStandards_Speed", "[StandardSpeedPerMinute] > 0");
                    table.ForeignKey(
                        name: "FK_WorkUnitStandards_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkUnitStandards_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OeeAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OeeAlerts", x => x.Id);
                    table.CheckConstraint("CK_OeeAlerts_Ratios", "[ActualValue] BETWEEN 0 AND 1 AND [TargetValue] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_OeeAlerts_OeeSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "OeeSnapshots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OeeAlerts_OeeTargets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "OeeTargets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OeeAlerts_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CounterCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlcDeviceMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastTotalCount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LastDefectCount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LastReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CounterEpoch = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CounterCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CounterCheckpoints_PlcDeviceMappings_PlcDeviceMappingId",
                        column: x => x.PlcDeviceMappingId,
                        principalTable: "PlcDeviceMappings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpcUaTagMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlcDeviceMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagRole = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    NodeId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcUaTagMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpcUaTagMappings_PlcDeviceMappings_PlcDeviceMappingId",
                        column: x => x.PlcDeviceMappingId,
                        principalTable: "PlcDeviceMappings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlcDeviceRuntimeStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlcDeviceMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectivityState = table.Column<int>(type: "int", nullable: false),
                    OperationalState = table.Column<int>(type: "int", nullable: false),
                    LastDataAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTransitionAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFaultCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlcDeviceRuntimeStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlcDeviceRuntimeStates_PlcDeviceMappings_PlcDeviceMappingId",
                        column: x => x.PlcDeviceMappingId,
                        principalTable: "PlcDeviceMappings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlcIntegrationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlcDeviceMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlcIntegrationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlcIntegrationEvents_PlcDeviceMappings_PlcDeviceMappingId",
                        column: x => x.PlcDeviceMappingId,
                        principalTable: "PlcDeviceMappings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductionOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StandardSpeedPerMinute = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalProducedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DefectQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnscheduledMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOutputs", x => x.Id);
                    table.CheckConstraint("CK_ProductionOutputs_Durations", "[UnscheduledMinutes] >= 0 AND [StandardSpeedPerMinute] > 0");
                    table.CheckConstraint("CK_ProductionOutputs_Period", "[PeriodEnd] > [PeriodStart]");
                    table.CheckConstraint("CK_ProductionOutputs_Quantities", "[TotalProducedQuantity] >= 0 AND [DefectQuantity] >= 0 AND [DefectQuantity] <= [TotalProducedQuantity]");
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_ProductionPlans_ProductionPlanId",
                        column: x => x.ProductionPlanId,
                        principalTable: "ProductionPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductionReconciliationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TotalDelta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DefectDelta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ResolvedProductionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReconciliationItems", x => x.Id);
                    table.CheckConstraint("CK_ProductionReconciliationItems_Deltas", "[TotalDelta] >= 0 AND [DefectDelta] >= 0 AND [DefectDelta] <= [TotalDelta]");
                    table.ForeignKey(
                        name: "FK_ProductionReconciliationItems_ProductionPlans_ResolvedProductionPlanId",
                        column: x => x.ResolvedProductionPlanId,
                        principalTable: "ProductionPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionReconciliationItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionReconciliationItems_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DowntimeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductionOutputId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DowntimeReasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DowntimeEvents", x => x.Id);
                    table.CheckConstraint("CK_DowntimeEvents_Period", "[EndAt] IS NULL OR [EndAt] > [StartAt]");
                    table.ForeignKey(
                        name: "FK_DowntimeEvents_DowntimeReasons_DowntimeReasonId",
                        column: x => x.DowntimeReasonId,
                        principalTable: "DowntimeReasons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DowntimeEvents_ProductionOutputs_ProductionOutputId",
                        column: x => x.ProductionOutputId,
                        principalTable: "ProductionOutputs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DowntimeEvents_WorkUnits_WorkUnitId",
                        column: x => x.WorkUnitId,
                        principalTable: "WorkUnits",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterCheckpoints_PlcDeviceMappingId",
                table: "CounterCheckpoints",
                column: "PlcDeviceMappingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_DowntimeReasonId_StartAt",
                table: "DowntimeEvents",
                columns: new[] { "DowntimeReasonId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_ProductionOutputId",
                table: "DowntimeEvents",
                column: "ProductionOutputId");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_WorkUnitId",
                table: "DowntimeEvents",
                column: "WorkUnitId",
                unique: true,
                filter: "[EndAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_WorkUnitId_StartAt_EndAt",
                table: "DowntimeEvents",
                columns: new[] { "WorkUnitId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeReasons_Code",
                table: "DowntimeReasons",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Enterprises_Code",
                table: "Enterprises",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OeeAlerts_SnapshotId_Metric",
                table: "OeeAlerts",
                columns: new[] { "SnapshotId", "Metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OeeAlerts_State_Severity_TriggeredAt",
                table: "OeeAlerts",
                columns: new[] { "State", "Severity", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OeeAlerts_TargetId",
                table: "OeeAlerts",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_OeeAlerts_WorkUnitId",
                table: "OeeAlerts",
                column: "WorkUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OeeSnapshots_WorkUnitId_PeriodType_PeriodCode_PeriodStart_PeriodEnd_CalculationVersion",
                table: "OeeSnapshots",
                columns: new[] { "WorkUnitId", "PeriodType", "PeriodCode", "PeriodStart", "PeriodEnd", "CalculationVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OeeTargets_SiteId",
                table: "OeeTargets",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_OeeTargets_WorkUnitId_SiteId_PeriodType_PeriodCode_EffectiveFrom",
                table: "OeeTargets",
                columns: new[] { "WorkUnitId", "SiteId", "PeriodType", "PeriodCode", "EffectiveFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OpcUaConnections_Code",
                table: "OpcUaConnections",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OpcUaTagMappings_PlcDeviceMappingId_TagRole_Direction",
                table: "OpcUaTagMappings",
                columns: new[] { "PlcDeviceMappingId", "TagRole", "Direction" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_OccurredAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlcDeviceMappings_DefaultProductId",
                table: "PlcDeviceMappings",
                column: "DefaultProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PlcDeviceMappings_OpcUaConnectionId_ExternalDeviceId",
                table: "PlcDeviceMappings",
                columns: new[] { "OpcUaConnectionId", "ExternalDeviceId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlcDeviceMappings_WorkUnitId",
                table: "PlcDeviceMappings",
                column: "WorkUnitId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlcDeviceRuntimeStates_PlcDeviceMappingId",
                table: "PlcDeviceRuntimeStates",
                column: "PlcDeviceMappingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlcFaultCodeMappings_DowntimeReasonId",
                table: "PlcFaultCodeMappings",
                column: "DowntimeReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_PlcFaultCodeMappings_FaultCode",
                table: "PlcFaultCodeMappings",
                column: "FaultCode",
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1 AND [WorkUnitId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlcFaultCodeMappings_WorkUnitId_FaultCode",
                table: "PlcFaultCodeMappings",
                columns: new[] { "WorkUnitId", "FaultCode" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1 AND [WorkUnitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlcIntegrationEvents_OccurredAt_Level",
                table: "PlcIntegrationEvents",
                columns: new[] { "OccurredAt", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_PlcIntegrationEvents_PlcDeviceMappingId_OccurredAt",
                table: "PlcIntegrationEvents",
                columns: new[] { "PlcDeviceMappingId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlcSessionEvents_OpcUaConnectionId_OccurredAt",
                table: "PlcSessionEvents",
                columns: new[] { "OpcUaConnectionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_SiteId_Code",
                table: "Processes",
                columns: new[] { "SiteId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_ProductId",
                table: "ProductionOutputs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_ProductionPlanId",
                table: "ProductionOutputs",
                column: "ProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_ShiftId",
                table: "ProductionOutputs",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_WorkUnitId_PeriodStart_PeriodEnd",
                table: "ProductionOutputs",
                columns: new[] { "WorkUnitId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_Code",
                table: "ProductionPlans",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_ProductId",
                table: "ProductionPlans",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_ShiftId",
                table: "ProductionPlans",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_WorkUnitId_PlannedStart_PlannedEnd",
                table: "ProductionPlans",
                columns: new[] { "WorkUnitId", "PlannedStart", "PlannedEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReconciliationItems_ProductId",
                table: "ProductionReconciliationItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReconciliationItems_ResolvedProductionPlanId",
                table: "ProductionReconciliationItems",
                column: "ResolvedProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReconciliationItems_SourceKey",
                table: "ProductionReconciliationItems",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReconciliationItems_State_CapturedAt",
                table: "ProductionReconciliationItems",
                columns: new[] { "State", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReconciliationItems_WorkUnitId",
                table: "ProductionReconciliationItems",
                column: "WorkUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                table: "Products",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SampleProductionPlanItems_ProductId",
                table: "SampleProductionPlanItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleProductionPlanItems_SampleProductionPlanId_WorkUnitId_ProductId_ShiftId",
                table: "SampleProductionPlanItems",
                columns: new[] { "SampleProductionPlanId", "WorkUnitId", "ProductId", "ShiftId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SampleProductionPlanItems_ShiftId",
                table: "SampleProductionPlanItems",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleProductionPlanItems_WorkUnitId",
                table: "SampleProductionPlanItems",
                column: "WorkUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleProductionPlans_Code",
                table: "SampleProductionPlans",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_SiteId_Code",
                table: "Shifts",
                columns: new[] { "SiteId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_EnterpriseId_Code",
                table: "Sites",
                columns: new[] { "EnterpriseId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkUnitGroups_ProcessId_Code",
                table: "WorkUnitGroups",
                columns: new[] { "ProcessId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkUnits_WorkUnitGroupId_Code",
                table: "WorkUnits",
                columns: new[] { "WorkUnitGroupId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkUnitStandards_ProductId",
                table: "WorkUnitStandards",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkUnitStandards_WorkUnitId_ProductId_EffectiveFrom",
                table: "WorkUnitStandards",
                columns: new[] { "WorkUnitId", "ProductId", "EffectiveFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [TR_WorkUnitStandards_PreventEffectiveOverlap]
                ON [WorkUnitStandards]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [WorkUnitStandards] e
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

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [TR_OeeTargets_PreventEffectiveOverlap]
                ON [OeeTargets]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [OeeTargets] e
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_WorkUnitStandards_PreventEffectiveOverlap]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_OeeTargets_PreventEffectiveOverlap]");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CounterCheckpoints");

            migrationBuilder.DropTable(
                name: "DowntimeEvents");

            migrationBuilder.DropTable(
                name: "OeeAlerts");

            migrationBuilder.DropTable(
                name: "OpcUaTagMappings");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PlcDeviceRuntimeStates");

            migrationBuilder.DropTable(
                name: "PlcFaultCodeMappings");

            migrationBuilder.DropTable(
                name: "PlcIntegrationEvents");

            migrationBuilder.DropTable(
                name: "PlcSessionEvents");

            migrationBuilder.DropTable(
                name: "ProductionReconciliationItems");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "SampleProductionPlanItems");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "WorkUnitStandards");

            migrationBuilder.DropTable(
                name: "ProductionOutputs");

            migrationBuilder.DropTable(
                name: "OeeSnapshots");

            migrationBuilder.DropTable(
                name: "OeeTargets");

            migrationBuilder.DropTable(
                name: "DowntimeReasons");

            migrationBuilder.DropTable(
                name: "PlcDeviceMappings");

            migrationBuilder.DropTable(
                name: "SampleProductionPlans");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ProductionPlans");

            migrationBuilder.DropTable(
                name: "OpcUaConnections");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "WorkUnits");

            migrationBuilder.DropTable(
                name: "WorkUnitGroups");

            migrationBuilder.DropTable(
                name: "Processes");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "Enterprises");
        }
    }
}
