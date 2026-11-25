using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting.Importing.FaultMappings.Common;

namespace Oee.Application.Reporting.Importing.FaultMappings.Preview;

/// <summary>Điều phối thao tác PreviewFaultMappings trong cùng scope lưu dữ liệu.</summary>
internal sealed class PreviewFaultMappingsHandler(FaultMappingImportContext context)
    : IRequestHandler<PreviewFaultMappingsQuery, FaultMappingImportPreview>
{
    public async Task<FaultMappingImportPreview> HandleAsync(
        PreviewFaultMappingsQuery command,
        CancellationToken cancellationToken)
    {
        var xlsx = command.Xlsx;
        var length = command.Length;
        var document = await context.Workbook.ReadAsync(xlsx, length, cancellationToken);

        if (document.Issues.Count > 0)
            return new(FaultMappingImportContext.Version, document.TotalRows, [], document.Issues);
        // Tải lookup một lần thành dictionary để không query SQL theo từng dòng XLSX.
        var workUnits = await context.Store.ReadWorkUnitIdsAsync(false, cancellationToken);
        var reasons = await context.Store.ReadReasonIdsAsync(false, cancellationToken);
        var valid = new List<FaultMappingImportRow>();
        var issues = new List<FaultMappingImportIssue>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var faultScopes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // Mỗi row được normalize, validate lookup/format và thu thập lỗi độc lập cho dry-run UI.
        foreach (var source in document.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = source.RowNumber;
            var workUnit = ResolveCode(source.WorkUnitCode, workUnits);
            var fault = FaultMappingImportContext.Clean(source.FaultCode);
            var reason = ResolveCode(source.ReasonCode, reasons);
            var description = FaultMappingImportContext.Clean(source.Description);

            if (string.IsNullOrWhiteSpace(fault) && string.IsNullOrWhiteSpace(reason)
                && string.IsNullOrWhiteSpace(workUnit))
                continue;

            if (fault.Length is 0 or > 64)
                issues.Add(new(row, "FaultCode", "RequiredOrLength", "FaultCode bắt buộc và tối đa 64 ký tự."));

            if (!string.IsNullOrEmpty(workUnit) && !workUnits.ContainsKey(workUnit))
                issues.Add(new(row, "WorkUnitCode", "Unknown", "Không tìm thấy WorkUnit."));

            if (!reasons.ContainsKey(reason))
                issues.Add(new(row, "ReasonCode", "Unknown", "Không tìm thấy DowntimeReason."));
            var scope = string.IsNullOrEmpty(workUnit) ? "GLOBAL" : workUnit;
            var key = scope + "|" + fault;

            if (!keys.Add(key))
                issues.Add(new(row, "FaultCode", "Duplicate", "Trùng scope và FaultCode trong file."));

            if (!faultScopes.TryGetValue(fault, out var scopes))
                faultScopes[fault] = scopes = new(StringComparer.OrdinalIgnoreCase);
            scopes.Add(scope);
            var activeText = FaultMappingImportContext.Clean(source.IsActiveText);
            var active = activeText.Length == 0 || activeText.Equals("true", StringComparison.OrdinalIgnoreCase)
                || activeText == "1" || activeText.Equals("yes", StringComparison.OrdinalIgnoreCase);

            if (activeText.Length > 0 && activeText is not "0"
                && !activeText.Equals("false", StringComparison.OrdinalIgnoreCase)
                && !activeText.Equals("true", StringComparison.OrdinalIgnoreCase)
                && !activeText.Equals("yes", StringComparison.OrdinalIgnoreCase)
                && !activeText.Equals("no", StringComparison.OrdinalIgnoreCase)
                && activeText is not "1")
                issues.Add(new(row, "IsActive", "Boolean", "Chỉ nhận true/false hoặc 1/0."));

            if (!issues.Any(x => x.RowNumber == row))
                valid.Add(new(
                        row,
                        workUnit.Length == 0 ? null : workUnit,
                        fault,
                        reason,
                        description.Length == 0 ? null : description,
                        active));
        }

        foreach (var pair in faultScopes.Where(x => x.Value.Contains("GLOBAL") && x.Value.Count > 1))
            foreach (var row in valid.Where(x => x.FaultCode.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)))
                issues.Add(new(
                        row.RowNumber,
                        "WorkUnitCode",
                        "GlobalDeviceConflict",
                        "Cùng FaultCode xuất hiện ở scope global và device; hãy import ở các batch riêng sau khi review precedence."));
        valid.RemoveAll(row => issues.Any(x => x.RowNumber == row.RowNumber));

        return new(FaultMappingImportContext.Version, document.TotalRows, valid, issues);
    }

    private static string ResolveCode(string value, Dictionary<string, Guid> lookup)
    {
        var text = value.Trim();
        if (lookup.ContainsKey(text) || !Guid.TryParse(text, out var id)) return text;
        return lookup.FirstOrDefault(pair => pair.Value == id).Key ?? text;
    }
}
