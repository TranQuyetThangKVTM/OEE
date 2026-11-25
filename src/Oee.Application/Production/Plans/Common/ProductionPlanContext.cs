using Oee.Domain.Common;
using System.Data;
using System.Text.Json;
using Oee.Application.Audit;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Security;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Organization;
using Oee.Domain.Platform;

namespace Oee.Application.Production.Plans.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác ProductionPlan.</summary>
internal sealed partial class ProductionPlanContext(
    IUseCaseExecutor requests,
    IProductionPlanStore store,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor? currentActor = null,
    ICapabilityChecker? capabilityChecker = null)
    : IUseCaseContext
{
    internal IUseCaseExecutor Requests => requests;
    internal IProductionPlanStore Store => store;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal IClock Clock => clock;

    internal void EnsureAuthorized()
    {
        if (capabilityChecker is not null && !capabilityChecker.HasCapability(Capabilities.ProductionPlanManage))
        {
            throw new UnauthorizedAccessException("The current user is not authorized to manage production plans.");
        }
    }

    internal async Task<List<ProductionPlanCandidate>> BuildCandidatesAsync(
        BulkProductionPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AppliedDate == default)
        {
            throw new ArgumentException("Ngày áp dụng là bắt buộc.", nameof(request));
        }

        return request.Mode switch
        {
            BulkPlanCreationModeContract.Direct => await BuildDirectCandidatesAsync(request, cancellationToken),
            BulkPlanCreationModeContract.FromSample => await BuildSampleCandidatesAsync(request, cancellationToken),
            _ => throw new ArgumentException("Phương thức tạo kế hoạch không hợp lệ.", nameof(request)),
        };
    }

    internal async Task<List<ProductionPlanCandidate>> BuildDirectCandidatesAsync(
        BulkProductionPlanRequest request,
        CancellationToken cancellationToken)
    {
        var workUnitIds = request.WorkUnitIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        var shiftIds = request.ShiftIds.Where(x => x != Guid.Empty).Distinct().ToArray();

        if (workUnitIds.Length == 0 || shiftIds.Length == 0 || !request.ProductId.HasValue
            || request.ProductId == Guid.Empty
            || request.PlannedQuantity is null or <= 0)
        {
            throw new ArgumentException("Phải chọn thiết bị, ca, SKU và sản lượng kế hoạch lớn hơn 0.", nameof(request));
        }

        // Tải một lần toàn bộ lookup và parent để kiểm tra active/site, không query trong vòng lặp.
        var units = await store.ReadActiveUnitsAsync(workUnitIds, cancellationToken);
        var shifts = await store.ReadActiveShiftsAsync(shiftIds, cancellationToken);
        var product = await store.FindActiveProductAsync(request.ProductId.Value, cancellationToken)
            ?? throw new KeyNotFoundException("SKU không tồn tại hoặc không còn Active.");

        if (units.Length != workUnitIds.Length)
        {
            throw new KeyNotFoundException("Một hoặc nhiều thiết bị không tồn tại hoặc không còn Active.");
        }

        if (shifts.Length != shiftIds.Length)
        {
            throw new KeyNotFoundException("Một hoặc nhiều ca không tồn tại hoặc không còn Active.");
        }

        // Capacity đúng kích thước tích giúp tránh cấp phát lại khi dựng danh sách.
        var candidates = new List<ProductionPlanCandidate>(units.Length * shifts.Length);

        foreach (var unit in units)
        {
            foreach (var shift in shifts)
            {
                if (unit.WorkCenter.Area.SiteId != shift.SiteId)
                {
                    candidates.Add(ProductionPlanCandidate.Invalid(
                            unit,
                            shift,
                            product,
                            request.PlannedQuantity.Value,
                            "Thiết bị và ca không thuộc cùng nhà máy."));
                    continue;
                }

                var period = ResolvePeriod(request.AppliedDate, shift);
                candidates.Add(ProductionPlanCandidate.Valid(
                        unit,
                        shift,
                        product,
                        request.PlannedQuantity.Value,
                        period.Start,
                        period.End,
                        null,
                        null,
                        NullIfWhiteSpace(request.Notes)));
            }
        }

        return candidates;
    }

    internal async Task<List<ProductionPlanCandidate>> BuildSampleCandidatesAsync(
        BulkProductionPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.SamplePlanId.HasValue || request.SamplePlanId == Guid.Empty)
        {
            throw new ArgumentException("Kế hoạch mẫu là bắt buộc.", nameof(request));
        }

        _ = await store.FindSampleAsync(request.SamplePlanId.Value, cancellationToken)
            ?? throw new KeyNotFoundException("Kế hoạch mẫu không tồn tại hoặc đã ngừng sử dụng.");
        var items = await store.ReadSampleItemsAsync(request.SamplePlanId.Value, cancellationToken);

        if (items.Length == 0)
        {
            throw new InvalidOperationException("Kế hoạch mẫu chưa có dòng hoạt động.");
        }

        // Override chỉ tồn tại trong request tạo plan; entity dòng mẫu không bị cập nhật.
        var overrides = (request.QuantityOverrides ?? []).GroupBy(x => x.SamplePlanItemId)
            .ToDictionary(
            x => x.Key,
            x => x.Last().PlannedQuantity);

        if (overrides.Keys.Except(items.Select(x => x.Id)).Any())
        {
            throw new ArgumentException("Bản chỉnh sản lượng chứa dòng không thuộc kế hoạch mẫu.", nameof(request));
        }

        var candidates = new List<ProductionPlanCandidate>(items.Length);

        foreach (var item in items)
        {
            var quantity = overrides.GetValueOrDefault(item.Id, item.PlannedQuantity);
            var active = item.WorkUnit.Status == RecordStatusEnum.Active
                && item.WorkUnit.WorkCenter.Status == RecordStatusEnum.Active
                && item.WorkUnit.WorkCenter.Area.Status == RecordStatusEnum.Active
                && item.WorkUnit.WorkCenter.Area.Site.Status == RecordStatusEnum.Active
                && item.Shift.Status == RecordStatusEnum.Active
                && item.Shift.Site.Status == RecordStatusEnum.Active
                && item.Product.Status == RecordStatusEnum.Active;

            if (!active || quantity <= 0 || item.WorkUnit.WorkCenter.Area.SiteId != item.Shift.SiteId)
            {
                var reason = !active
                    ? "Thiết bị, ca hoặc SKU của dòng mẫu không còn Active."
                    : quantity <= 0 ? "Sản lượng kế hoạch phải lớn hơn 0." : "Thiết bị và ca không thuộc cùng nhà máy.";
                candidates.Add(ProductionPlanCandidate.Invalid(
                        item.WorkUnit,
                        item.Shift,
                        item.Product,
                        quantity,
                        reason,
                        item.SampleProductionPlanId,
                        item.Id));
                continue;
            }

            var period = ResolvePeriod(request.AppliedDate, item.Shift);
            candidates.Add(ProductionPlanCandidate.Valid(
                    item.WorkUnit,
                    item.Shift,
                    item.Product,
                    quantity,
                    period.Start,
                    period.End,
                    item.SampleProductionPlanId,
                    item.Id,
                    NullIfWhiteSpace(request.Notes) ?? item.Notes));
        }

        return candidates;
    }

    internal async Task<List<ProductionPlanCandidate>> MarkExistingAsync(
        List<ProductionPlanCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var valid = candidates.Where(x => x.State == "Valid").ToArray();

        if (valid.Length == 0)
        {
            return candidates;
        }

        var unitIds = valid.Select(x => x.WorkUnitId).Distinct().ToArray();
        var minimum = valid.Min(x => x.StartAt);
        var maximum = valid.Max(x => x.EndAt);
        // Một query rộng theo min/max rồi đối chiếu trong memory, tránh query SQL cho từng candidate.
        var existing = await store.ReadOverlappingPlansAsync(unitIds, minimum, maximum, cancellationToken);

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];

            if (candidate.State != "Valid")
            {
                continue;
            }

            var collisions = existing.Where(x => x.WorkUnitId == candidate.WorkUnitId && x.PlannedStart < candidate.EndAt
                    && x.PlannedEnd > candidate.StartAt)
                .ToArray();

            if (collisions.Length == 0)
            {
                continue;
            }

            var duplicate = collisions.Any(x => x.ProductId == candidate.ProductId && x.PlannedStart == candidate.StartAt
                    && x.PlannedEnd == candidate.EndAt);
            candidates[index] = candidate with
            {
                State = duplicate ? "Skipped" : "Conflict",
                Reason = duplicate
                    ? "Tổ hợp thiết bị × ca × SKU trong ngày đã có kế hoạch."
                    : "Thiết bị và ca trong ngày đã có kế hoạch với SKU hoặc khung giờ khác; không ghi đè.",
            };
        }

        var accepted = new List<ProductionPlanCandidate>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];

            if (candidate.State != "Valid")
                continue;

            if (accepted.Any(x => x.WorkUnitId == candidate.WorkUnitId && x.StartAt < candidate.EndAt && x.EndAt > candidate.StartAt))
                candidates[index] = candidate with
                {
                    State = "Conflict",
                    Reason = "Các ca được chọn trùng thời gian trên cùng thiết bị."
                };
            else
                accepted.Add(candidate);
        }

        return candidates;
    }

    internal static BulkProductionPlanPreview Preview(List<ProductionPlanCandidate> candidates)
        => new(
            candidates.Count,
            candidates.Count(x => x.State == "Valid"),
            candidates.Count(x => x.State == "Skipped"),
            candidates.Count(x => x.State == "Conflict"),
            candidates.Select(ToContract).ToArray());

    internal AuditLogEntity CreateBulkAudit(
        BulkProductionPlanRequest request,
        ProductionPlanEntity[] plans,
        List<ProductionPlanCandidate> candidates)
        => AuditLogEntity.Create(
            timestamp: clock.UtcNow,
            action: "BulkProductionPlansCreated",
            entityType: "ProductionPlan",
            entityId: request.SamplePlanId?.ToString()
                ?? plans.FirstOrDefault()?.Id.ToString() ?? $"bulk:{request.AppliedDate:yyyyMMdd}",
            userId: currentActor?.UserId,
            userName: currentActor?.UserName ?? "system",
            entitySubject: request.Mode.ToString(),
            changes: JsonSerializer.Serialize(new
                {
                    request.Mode,
                    SourceSampleProductionPlanId = request.SamplePlanId,
                    RequestedCount = candidates.Count,
                    CreatedCount = plans.Length,
                    SkippedCount = candidates.Count(x => x.State == "Skipped"),
                    ConflictCount = candidates.Count(x => x.State == "Conflict"),
                    CreatedPlanIds = plans.Select(x => x.Id),

                }),
            ipAddress: AuditIpAddress.Normalize(currentActor?.IpAddress));

    internal static (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod(
        DateOnly date,
        ShiftEntity shift)
    {
        TimeZoneInfo zone;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(shift.Site.TimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"Múi giờ '{shift.Site.TimeZoneId}' của nhà máy không hợp lệ.", exception);
        }

        var period = ShiftPeriodResolver.Resolve(date, shift.StartTime, shift.EndTime, zone);

        return (period.Start, period.End);
    }

    internal async Task<ProductionPlanEntity> FindAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await store.FindCurrentAsync(id, true, cancellationToken)
            ?? throw new KeyNotFoundException($"Production plan '{id}' was not found.");

    internal async Task EnsureActiveWindowAsync(
        ProductionPlanRequest request,
        Guid? replacingId,
        CancellationToken cancellationToken)
    {
        var siteId = await store.ReadActiveUnitSiteAsync(request.WorkUnitId, cancellationToken);

        if (siteId is null || !await store.IsActiveProductAsync(request.ProductId, cancellationToken))
            throw new InvalidOperationException("Thiết bị hoặc sản phẩm chưa hoạt động.");

        if (request.ShiftId.HasValue
            && !await store.IsActiveShiftForSiteAsync(request.ShiftId.Value, siteId.Value, cancellationToken))
            throw new InvalidOperationException("Ca làm việc phải hoạt động và thuộc cùng nhà máy với thiết bị.");

        if (await store.HasOverlappingPlanAsync(
            request.WorkUnitId,
            replacingId,
            request.PlannedStart,
            request.PlannedEnd,
            cancellationToken))
            throw new InvalidOperationException("Thiết bị đã có kế hoạch có hiệu lực trùng thời gian.");
    }

    internal static ProductionPlanEntity MapNew(ProductionPlanRequest request)
        => ProductionPlanEntity.Create(
            request.WorkUnitId,
            request.ProductId,
            request.Code,
            request.PlannedStart,
            request.PlannedEnd,
            request.PlannedQuantity,
            request.ShiftId,
            request.Notes);

    internal static string BuildPlanCode(
        ProductionPlanCandidate candidate,
        DateOnly appliedDate,
        HashSet<string> reservedCodes)
    {
        // Giữ giới hạn Code 32 ký tự của database hiện hữu; chừa chỗ cho hậu tố khi trùng mã.
        for (var number = 1; ; number++)
        {
            var suffix = number == 1 ? string.Empty : FormattableString.Invariant($"-{number}");
            var code = ProductionPlanCode.Format(
                candidate.ProductCode,
                appliedDate,
                candidate.ShiftCode,
                candidate.WorkUnitName,
                suffix);
            if (reservedCodes.Add(code))
            {
                return code;
            }
        }
    }

    internal static void Apply(
        ProductionPlanEntity plan,
        ProductionPlanRequest request)
        => plan.Update(
            request.WorkUnitId,
            request.ProductId,
            request.Code,
            request.PlannedStart,
            request.PlannedEnd,
            request.PlannedQuantity,
            request.ShiftId,
            request.Notes);

    internal static ProductionPlanResponse Map(
        ProductionPlanEntity plan,
        bool hasOutput)
        => new(
            plan.Id,
            plan.VersionSeriesId,
            plan.Version,
            plan.WorkUnitId,
            plan.ProductId,
            plan.ShiftId,
            plan.Code,
            plan.PlannedStart,
            plan.PlannedEnd,
            plan.PlannedQuantity,
            plan.Notes,
            Convert.ToBase64String(plan.RowVersion),
            hasOutput,
            plan.SourceSampleProductionPlanId,
            plan.SourceSampleProductionPlanItemId);

    internal static BulkPlanCombination ToContract(ProductionPlanCandidate candidate)
        => new(
            candidate.WorkUnitId,
            candidate.WorkUnit,
            candidate.ShiftId,
            candidate.Shift,
            candidate.ProductId,
            candidate.Product,
            candidate.PlannedQuantity,
            candidate.StartAt,
            candidate.EndAt,
            candidate.SourceSamplePlanItemId,
            candidate.State,
            candidate.Reason);

    internal static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
