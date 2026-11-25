using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Reporting;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.Reporting;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class SourceFaultWorkbookTests
{
    [Fact]
    public async Task LegacyIdsResolveToCurrentCodesDuringPreviewWithoutWriting()
    {
        var unitId = Guid.NewGuid();
        var reasonId = Guid.NewGuid();
        var store = new LookupStore(unitId, reasonId);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Mappings");
        sheet.Cell(1, 1).Value = "WorkUnitId";
        sheet.Cell(1, 2).Value = "FaultCode";
        sheet.Cell(1, 3).Value = "DowntimeReasonId";
        sheet.Cell(2, 1).Value = unitId.ToString();
        sheet.Cell(2, 2).Value = 30000008;
        sheet.Cell(2, 3).Value = reasonId.ToString();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        // Preview uses the fake lookup store; this DbContext never opens a connection.
        using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer("Server=localhost;Database=UnusedPreviewOnly;Integrated Security=True;Encrypt=False").Options);
        var scenario = new FaultMappingImportScenario(new FaultMappingWorkbook(Options.Create(new ReportingOptions())),
            store, new EfApplicationUnitOfWork(db));
        var preview = await scenario.PreviewAsync(stream, stream.Length, CancellationToken.None);
        Assert.Empty(preview.Issues);
        var row = Assert.Single(preview.ValidRows);
        Assert.Equal("M-001", row.WorkUnitCode);
        Assert.Equal("DR-MOTOR", row.ReasonCode);
        Assert.Equal("30000008", row.FaultCode);
        Assert.False(store.Written);
    }

    [Fact]
    public async Task ImportedSourceWorkbookAcceptsIdHeadersInAnyOrderAndNormalizesNumericFaults()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Mappings");
        sheet.Cell(1, 1).Value = "DowntimeReasonId";
        sheet.Cell(1, 2).Value = "FaultCode";
        sheet.Cell(1, 3).Value = "WorkUnitId";
        sheet.Cell(1, 4).Value = "IsActive";
        sheet.Cell(1, 5).Value = "IsDeleted";
        var reason = Guid.NewGuid().ToString();
        var unit = Guid.NewGuid().ToString();
        sheet.Cell(2, 1).Value = reason;
        sheet.Cell(2, 2).Value = 30000008d;
        sheet.Cell(2, 3).Value = unit;
        sheet.Cell(2, 4).Value = true;
        sheet.Cell(3, 1).Value = reason;
        sheet.Cell(3, 2).Value = "old";
        sheet.Cell(3, 5).Value = true;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var reader = new FaultMappingWorkbook(Options.Create(new ReportingOptions()));
        var result = await reader.ReadAsync(stream, stream.Length, CancellationToken.None);
        Assert.Empty(result.Issues);
        var row = Assert.Single(result.Rows);
        Assert.Equal("30000008", row.FaultCode);
        Assert.Equal(reason, row.ReasonCode);
        Assert.Equal(unit, row.WorkUnitCode);
        Assert.Equal("true", row.IsActiveText);
    }

    [Fact]
    public async Task SourceWorkbookSupportsGlobalMappingAndRejectsFormulasBeforeImport()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Mappings");
        sheet.Cell(1, 1).Value = "FaultCode";
        sheet.Cell(1, 2).Value = "DowntimeReasonId";
        sheet.Cell(2, 1).Value = "0008";
        sheet.Cell(2, 2).Value = Guid.NewGuid().ToString();
        var reader = new FaultMappingWorkbook(Options.Create(new ReportingOptions()));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var result = await reader.ReadAsync(stream, stream.Length, CancellationToken.None);
        Assert.Equal("0008", Assert.Single(result.Rows).FaultCode);
        Assert.Empty(Assert.Single(result.Rows).WorkUnitCode);
        sheet.Cell(2, 1).FormulaA1 = "1+1";
        using var withFormula = new MemoryStream();
        workbook.SaveAs(withFormula);
        withFormula.Position = 0;
        result = await reader.ReadAsync(withFormula, withFormula.Length, CancellationToken.None);
        Assert.Contains(result.Issues, issue => issue.Code == "Formula");
    }

    private sealed class LookupStore(Guid unitId, Guid reasonId) : IFaultMappingImportStore
    {
        public bool Written { get; private set; }
        public Task<Dictionary<string, Guid>> ReadWorkUnitIdsAsync(bool tracking, CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["M-001"] = unitId });
        public Task<Dictionary<string, Guid>> ReadReasonIdsAsync(bool tracking, CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["DR-MOTOR"] = reasonId });
        public Task<PlcFaultCodeMappingEntity?> FindIncludingDeletedAsync(Guid? unitId, string faultCode, CancellationToken ct) => throw new NotSupportedException();
        public void Add(PlcFaultCodeMappingEntity mapping) => Written = true;
    }
}
