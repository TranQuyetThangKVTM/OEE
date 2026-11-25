using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Infrastructure.Identity;
using Oee.Infrastructure.Persistence;

namespace Oee.DatabaseMigrator;

/// <summary>
/// Seed idempotent role, tài khoản quản trị development và dữ liệu master tối thiểu sau khi migration hoàn tất.
/// </summary>
/// <remarks>
/// Class này thuộc công cụ DatabaseMigrator, không được gọi tự động ở mọi lần Web/Worker startup.
/// Mật khẩu Admin phải đến từ <see cref="SeedOptions"/> qua secret/environment, không nằm trong migration.
/// </remarks>
public sealed class DatabaseSeeder(
    OeeDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<SeedOptions> options)
{
    private static readonly string[] Roles = ["Admin", "PlanningManager", "Operator", "Viewer"];

    /// <summary>Tạo các role, Admin và master mẫu nếu chúng chưa tồn tại.</summary>
    /// <param name="cancellationToken">Cho phép dừng các truy vấn/ghi EF Core khi tiến trình bị hủy.</param>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedAdministratorAsync();
        await SeedMasterDataAsync(cancellationToken);
    }

    /// <summary>Chỉ tạo roles/Admin cho database test mới, không thêm cây/ca/sản phẩm mẫu.</summary>
    public async Task SeedAdministratorAsync()
    {
        // Bước 1: dùng RoleManager để Identity chuẩn hóa tên và ghi đúng các bảng role liên quan.
        foreach (var roleName in Roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)), $"create role {roleName}");
            }
        }

        // Bước 2: user/role được xử lý bởi UserManager; master data được ghi qua OeeDbContext.
        await SeedAdminAsync();
    }

    /// <summary>Kiểm tra mật khẩu trước khi tạo database để lỗi nhập không để lại database khởi tạo dở.</summary>
    public async Task ValidateAdministratorPasswordAsync()
    {
        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminPassword)) throw new InvalidOperationException("Seed__AdminPassword is required.");
        var user = new ApplicationUser { UserName = seed.AdminUserName };
        foreach (var validator in userManager.PasswordValidators)
            EnsureSucceeded(await validator.ValidateAsync(userManager, user, seed.AdminPassword), "validate administrator password");
    }

    /// <summary>
    /// Đặt lại mật khẩu Admin development, mở khóa tài khoản và xóa bộ đếm đăng nhập sai.
    /// </summary>
    /// <exception cref="InvalidOperationException">Mật khẩu secret bị thiếu hoặc Identity từ chối thao tác.</exception>
    /// <exception cref="KeyNotFoundException">Không tìm thấy username Admin đã cấu hình.</exception>
    public async Task ResetAdminPasswordAsync()
    {
        // Không dùng giá trị mặc định cho mật khẩu; thiếu secret phải dừng rõ ràng.
        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            throw new InvalidOperationException("Seed__AdminPassword is required to reset the development administrator password.");
        }

        // Tải user qua normalized username và chạy chính PasswordValidators của server.
        var user = await userManager.FindByNameAsync(seed.AdminUserName)
            ?? throw new KeyNotFoundException($"Administrator '{seed.AdminUserName}' was not found.");
        foreach (var validator in userManager.PasswordValidators)
        {
            EnsureSucceeded(await validator.ValidateAsync(userManager, user, seed.AdminPassword), "validate development administrator password");
        }

        // Tạo hash bằng PasswordHasher của Identity; SecurityStamp mới làm session cũ mất hiệu lực theo cơ chế hiện có.
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, seed.AdminPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        EnsureSucceeded(await userManager.UpdateAsync(user), "reset development administrator password");
        EnsureSucceeded(await userManager.SetLockoutEndDateAsync(user, null), "unlock development administrator");
        EnsureSucceeded(await userManager.ResetAccessFailedCountAsync(user), "reset administrator access-failed count");
    }

    private async Task SeedAdminAsync()
    {
        // Admin chỉ được tạo khi secret có thật; không seed PasswordHash tĩnh vào migration.
        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            throw new InvalidOperationException("Seed__AdminPassword is required to create the development administrator.");
        }

        // Idempotency: nếu username đã tồn tại thì giữ user, chỉ bổ sung role Admin còn thiếu.
        var user = await userManager.FindByNameAsync(seed.AdminUserName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = seed.AdminUserName,
                Email = seed.AdminEmail,
                DisplayName = seed.AdminDisplayName,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            EnsureSucceeded(await userManager.CreateAsync(user, seed.AdminPassword), "create development administrator");
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, "Admin"), "assign Admin role");
        }
    }

    private async Task SeedMasterDataAsync(CancellationToken cancellationToken)
    {
        // Mọi timestamp audit seed dùng UTC; mỗi business code được tra trước để chạy lại không tạo duplicate.
        var now = DateTimeOffset.UtcNow;
        var enterprise = await dbContext.Enterprises.SingleOrDefaultAsync(x => x.Code == "MASAN", cancellationToken);
        if (enterprise is null)
        {
            enterprise = Created(EnterpriseEntity.Create("MASAN", "Masan"), now);
            dbContext.Add(enterprise);
        }

        var site = await dbContext.Sites.SingleOrDefaultAsync(x => x.Code == "SITE-01", cancellationToken);
        if (site is null)
        {
            site = Created(SiteEntity.Create(enterprise.Id, "SITE-01", "Nhà máy 01", "SE Asia Standard Time"), now);
            dbContext.Add(site);
        }

        var area = await dbContext.Areas.SingleOrDefaultAsync(x => x.Code == "PACK", cancellationToken);
        if (area is null)
        {
            area = Created(AreaEntity.Create(site.Id, "PACK", "Đóng gói"), now);
            dbContext.Add(area);
        }

        var group = await dbContext.WorkCenters.SingleOrDefaultAsync(x => x.Code == "LINE-01", cancellationToken);
        if (group is null)
        {
            group = Created(WorkCenterEntity.Create(area.Id, "LINE-01", "Dây chuyền 01"), now);
            dbContext.Add(group);
        }

        // Seed 11 WorkUnit mẫu theo dây chuyền tham chiếu; đây là Development seed có nhãn rõ.
        var units = new[]
        {
            ("M-001", "Blow Moulder"), ("M-002", "Contiflow"), ("M-003", "Filler"),
            ("M-004", "Labeller"), ("M-005", "Bottle Coder"), ("M-006", "Variopac"),
            ("M-007", "Block Picker"), ("M-008", "Carton Erector"), ("M-009", "Carton Coder"),
            ("M-010", "Carton Scale"), ("M-011", "Pallet Robot"),
        };
        foreach (var (code, name) in units)
        {
            if (!await dbContext.WorkUnits.AnyAsync(x => x.Code == code, cancellationToken))
            {
                dbContext.Add(Created(WorkUnitEntity.Create(group.Id, code, name), now));
            }
        }

        // TimeOnly chỉ biểu diễn giờ trong ngày; S3 có EndTime nhỏ hơn StartTime nên là ca qua nửa đêm.
        foreach (var shift in new[] { ("S1", "Ca sáng", new TimeOnly(6, 0), new TimeOnly(14, 0)), ("S2", "Ca chiều", new TimeOnly(14, 0), new TimeOnly(22, 0)), ("S3", "Ca đêm", new TimeOnly(22, 0), new TimeOnly(6, 0)) })
        {
            if (!await dbContext.Shifts.AnyAsync(x => x.Code == shift.Item1, cancellationToken))
            {
                dbContext.Add(Created(ShiftEntity.Create(site.Id, shift.Item1, shift.Item2, shift.Item3, shift.Item4), now));
            }
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Code == "SKU-001", cancellationToken);
        if (product is null)
        {
            product = Created(ProductEntity.Create("SKU-001", "Sản phẩm mẫu", "chai"), now);
            dbContext.Add(product);
        }

        // Category/Kind dùng enum Domain để MTBF/MTTR chỉ nhận đúng Breakdown/Failure.
        foreach (var reason in new[]
        {
            ("DR-UNKNOWN", "Chưa phân loại", DowntimeCategoryEnum.Unknown, DowntimeKindEnum.Unknown),
            ("DR-BREAKDOWN", "Hỏng máy", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown),
            ("DR-CIP", "Vệ sinh CIP", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Planned),
            ("DR-SETUP", "Chuyển đổi sản phẩm", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Planned),
            ("DR-MEAL", "Ăn ca", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Planned),
            ("DR-MAINT", "Bảo trì kế hoạch", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Planned),
            ("DR-MINOR", "Dừng ngắn", DowntimeCategoryEnum.PerformanceLoss, DowntimeKindEnum.MinorStop),
            ("DR-SPEED", "Giảm tốc", DowntimeCategoryEnum.PerformanceLoss, DowntimeKindEnum.MinorStop),
            ("DR-QUALITY", "Xử lý lỗi chất lượng", DowntimeCategoryEnum.QualityLoss, DowntimeKindEnum.Quality),
        })
        {
            if (!await dbContext.DowntimeReasons.AnyAsync(x => x.Code == reason.Item1, cancellationToken))
            {
                dbContext.Add(Created(DowntimeReasonEntity.Create(reason.Item1, reason.Item2, reason.Item3, reason.Item4), now));
            }
        }

        // Lưu cây cha/master trước để lấy đủ FK cho standard và target ở bước tiếp theo.
        await dbContext.SaveChangesAsync(cancellationToken);

        var workUnits = await dbContext.WorkUnits.ToListAsync(cancellationToken);
        foreach (var workUnit in workUnits)
        {
            if (!await dbContext.WorkUnitStandards.AnyAsync(x => x.WorkUnitId == workUnit.Id && x.ProductId == product.Id, cancellationToken))
            {
                dbContext.Add(Created(WorkUnitStandardEntity.Create(workUnit.Id, product.Id, 100m, product.DefaultUom, new DateOnly(2026, 1, 1), approvedBy: "DatabaseMigrator"), now));
            }
        }

        // Target PDF chỉ là fallback cho database hoàn toàn mới; không ghi đè cấu hình target đang có.
        if (!await dbContext.OeeTargets.AnyAsync(cancellationToken))
        {
            dbContext.Add(Created(OeeTargetEntity.Create(OeePeriodTypeEnum.Day, "DAY", .85m, .90m, .95m, .99m, new DateOnly(2026, 1, 1), siteId: site.Id, note: "Default only for a new database"), now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        // Chuyển nhiều lỗi typed của Identity thành lỗi công cụ có đủ mô tả để người vận hành xử lý.
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
    private static T Created<T>(T entity, DateTimeOffset at) where T : AuditedEntity
    {
        entity.RecordCreation(at, "DatabaseMigrator");
        return entity;
    }
}
