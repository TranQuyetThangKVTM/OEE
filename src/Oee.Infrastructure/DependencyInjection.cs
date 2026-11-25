using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Oee.Application.Audit;
using Oee.Application.Common;
using Oee.Application.Identity;
using Oee.Application.Modules;
using Oee.Application.Oee;
using Oee.Application.Operations;
using Oee.Application.PlcIntegration;
using Oee.Application.Reporting;
using Oee.Application.Security;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Identity;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.PlcIntegration;
using Oee.Infrastructure.Reporting;
using Oee.Infrastructure.Security;
using Oee.Infrastructure.UseCases;

namespace Oee.Infrastructure;

/// <summary>
/// Đăng ký các adapter Infrastructure vào Dependency Injection mà không tự chạy migration.
/// </summary>
/// <remarks>
/// Web và DatabaseMigrator là composition root gọi extension này. Các interface Application được nối với
/// implementation EF Core/Identity/Reporting tương ứng. Migration vẫn thuộc DatabaseMigrator hoặc
/// lệnh vận hành có kiểm soát, không chạy mù mỗi khi Web khởi động.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Đăng ký DbContext, Identity store, interceptor và toàn bộ application adapter.</summary>
    /// <param name="services">Service collection của composition root.</param>
    /// <param name="connectionString">SQL Server connection string lấy từ cấu hình/secret; null sẽ bỏ qua phần database.</param>
    /// <param name="includeSignInManager">Bật SignInManager cho host HTTP; công cụ migration có thể tắt.</param>
    /// <param name="includeIdentityStore">Bật Identity store của OEE khi OEE chạy độc lập. Host tích hợp tắt tùy chọn này để dùng Identity store chung.</param>
    /// <returns>Chính service collection để tiếp tục chuỗi đăng ký.</returns>
    public static IServiceCollection AddOeeInfrastructure(
        this IServiceCollection services,
        string? connectionString,
        bool includeSignInManager = true,
        bool includeIdentityStore = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Nhóm bảo mật/audit được scoped theo request hoặc một unit xử lý Worker.
        services.TryAddScoped<ICurrentActor, SystemCurrentActor>();
        services.AddDataProtection();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<ISaveChangesInterceptor>(provider => provider.GetRequiredService<AuditSaveChangesInterceptor>());
        if (includeIdentityStore)
        {
            services.AddScoped<IIdentityAccountGateway, IdentityAccountGateway>();
        }
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddOptions<AuditRetentionOptions>();
        // Clock không giữ trạng thái và có thể dùng chung; test có thể đăng ký fake trước TryAdd.
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<EfApplicationUnitOfWork>();
        services.AddScoped<IApplicationUnitOfWork>(provider => provider.GetRequiredService<EfApplicationUnitOfWork>());
        services.AddApplicationUseCases();
        services.AddOptions<Production.Automation.PlanAutomationStorageOptions>();
        services.AddSingleton<Oee.Application.Production.Automation.IProductionPlanScheduleStore,
            Production.Automation.JsonProductionPlanScheduleStore>();
        services.AddSingleton<Oee.Application.Production.Automation.IAutomaticShiftReportFiles,
            Production.Automation.AutomaticShiftReportFiles>();
        services.AddSingleton<Oee.Application.Production.LineOff.ILineOffCalendarStore,
            Production.LineOff.JsonLineOffCalendarStore>();
        services.AddScoped<Oee.Application.Production.LineOff.ILineOffScopeStore,
            Production.LineOff.EfLineOffScopeStore>();
        services.AddScoped<IProductionPlanStore, EfProductionPlanStore>();
        services.AddScoped<IProductionOutputStore, EfProductionOutputStore>();
        services.AddScoped<IDowntimeStore, EfDowntimeStore>();
        services.AddScoped<ISampleProductionPlanStore, EfSampleProductionPlanStore>();
        services.AddScoped<IMasterDataStore, EfMasterDataStore>();
        services.AddScoped<IPlcTelemetryStore, EfPlcTelemetryStore>();
        services.AddScoped<EfOeeStore>();
        services.AddScoped<IOeeSnapshotStore>(provider => provider.GetRequiredService<EfOeeStore>());
        services.AddScoped<IOeeAlertStore>(provider => provider.GetRequiredService<EfOeeStore>());
        services.AddScoped<IOeeTargetStore>(provider => provider.GetRequiredService<EfOeeStore>());
        services.AddScoped<IAlertReadStore, EfAlertReadStore>();
        services.AddScoped<IPlcConfigurationStore, EfPlcConfigurationStore>();
        services.AddScoped<IPlcConfigurationReadStore, EfPlcConfigurationReadStore>();
        services.AddScoped<IFaultMappingWorkbook, FaultMappingWorkbook>();
        services.AddScoped<IFaultMappingImportStore, EfFaultMappingImportStore>();
        services.AddScoped<IAuditRetentionStore, EfAuditRetentionStore>();
        services.AddScoped<AuditRetentionOptions>(provider => provider.GetRequiredService<IOptions<AuditRetentionOptions>>().Value);
        services.AddScoped<IOpcUaWriteBackStore, EfOpcUaWriteBackStore>();
        services.AddScoped<IOpcUaWriteBackSettings>(provider => provider.GetRequiredService<IOptions<BackgroundServices.Configuration.OpcUaWorkerOptions>>().Value);
        services.AddScoped<Oee.Application.Chat.IChatReadStore, Oee.Infrastructure.Chat.EfChatReadStore>();
        services.AddScoped<IOpcUiStatusService, OpcUiStatusService>();
        services.AddSingleton<Oee.Application.PlcIntegration.Monitoring.IOpcTagMonitorBuffer, OpcTagMonitorBuffer>();
        services.AddScoped<Oee.Application.PlcIntegration.Monitoring.IOpcTagMonitorStore, EfOpcTagMonitorStore>();
        services.AddScoped<ModuleReadServices>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<IProductionModuleQueryService, ProductionModuleQueryService>();
        services.AddScoped<IMasterDataQueryService, MasterDataQueryService>();
        services.AddScoped<IEquipmentHierarchyQueryService, EquipmentHierarchyQueryService>();
        services.AddScoped<IMasterOrganizationQueryService>(x => x.GetRequiredService<ModuleReadServices>());
        services.AddScoped<ILiveOeeQueryService, LiveOeeQueryService>();
        services.AddOptions<ReportingOptions>();
        services.AddScoped<IReportAnalysisQueryService, ReportAnalysisQueryService>();
        services.AddScoped<IReportExportService, ReportExportService>();

        // Chỉ cấu hình EF/Identity store khi composition root thực sự cung cấp connection string.
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // Interceptor tham gia cùng pipeline SaveChanges/transaction của EF Core.
            services.AddDbContext<OeeDbContext>(
                (provider, options) => options
                    .UseSqlServer(connectionString)
                    .AddInterceptors(provider.GetServices<ISaveChangesInterceptor>()),
                contextLifetime: ServiceLifetime.Transient,
                optionsLifetime: ServiceLifetime.Scoped);
            if (includeIdentityStore)
            {
                // OEE độc lập dùng Guid key và OeeDbContext làm Identity store.
                var identity = services
                    .AddIdentityCore<ApplicationUser>()
                    .AddRoles<IdentityRole<Guid>>()
                    .AddEntityFrameworkStores<OeeDbContext>()
                    .AddDefaultTokenProviders();
                // Identity và các store nghiệp vụ phải tham gia cùng transaction/audit của use case.
                services.AddScoped<IUserStore<ApplicationUser>>(provider =>
                    new UserStore<ApplicationUser, IdentityRole<Guid>, OeeDbContext, Guid>(
                        provider.GetRequiredService<EfApplicationUnitOfWork>().Db));
                if (includeSignInManager)
                {
                    identity.AddSignInManager();
                }
            }
        }

        return services;
    }
}
