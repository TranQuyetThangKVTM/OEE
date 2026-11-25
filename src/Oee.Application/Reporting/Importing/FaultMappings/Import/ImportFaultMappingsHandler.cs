using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting.Importing.FaultMappings.Common;
using Oee.Application.Reporting.Importing.FaultMappings.Preview;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.Reporting.Importing.FaultMappings.Import;

/// <summary>Điều phối thao tác ImportFaultMappings trong cùng scope lưu dữ liệu.</summary>
internal sealed class ImportFaultMappingsHandler(FaultMappingImportContext context)
    : IRequestHandler<ImportFaultMappingsCommand, FaultMappingImportResult>
{
    public async Task<FaultMappingImportResult> HandleAsync(
        ImportFaultMappingsCommand command,
        CancellationToken cancellationToken)
    {
        var xlsx = command.Xlsx;
        var length = command.Length;
        var preview = await context.Requests.ExecuteAsync(new PreviewFaultMappingsQuery(xlsx, length), cancellationToken);

        if (preview.Issues.Count > 0)
            return new(0, 0, 0, preview.Issues);
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var workUnits = await context.Store.ReadWorkUnitIdsAsync(true, cancellationToken);
        var reasons = await context.Store.ReadReasonIdsAsync(true, cancellationToken);

        // Không SaveChanges trong vòng lặp; mọi insert/update được flush một lần cuối transaction.
        foreach (var row in preview.ValidRows)
        {
            var workUnitId = row.WorkUnitCode is null ? (Guid?)null : workUnits[row.WorkUnitCode];
            var reasonId = reasons[row.ReasonCode];
            var current = await context.Store.FindIncludingDeletedAsync(workUnitId, row.FaultCode, cancellationToken);

            if (current is null)
            {
                context.Store.Add(PlcFaultCodeMappingEntity.Create(
                        faultCode: row.FaultCode,
                        downtimeReasonId: reasonId,
                        workUnitId: workUnitId,
                        description: row.Description,
                        isActive: row.IsActive));
                inserted++;
            }
            else if (!current.IsDeleted && current.DowntimeReasonId == reasonId && current.Description == row.Description
                && current.IsActive == row.IsActive)
                skipped++;
            else
            {
                current.RestoreMapping(row.FaultCode, reasonId, workUnitId, row.Description, row.IsActive);
                updated++;
            }
        }

        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(inserted, updated, skipped, []);
    }
}
