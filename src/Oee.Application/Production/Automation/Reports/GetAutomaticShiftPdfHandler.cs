using Oee.Application.Common;
using Oee.Application.Security;

namespace Oee.Application.Production.Automation;

internal sealed class GetAutomaticShiftPdfHandler(
    IProductionPlanScheduleStore schedules,
    IAutomaticShiftReportFiles files,
    ICapabilityChecker? capabilities = null)
    : IRequestHandler<GetAutomaticShiftPdfQuery, AutomaticShiftPdf>
{
    public async Task<AutomaticShiftPdf> HandleAsync(
        GetAutomaticShiftPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (capabilities?.HasCapability(Capabilities.ReportsRead) != true)
            throw new UnauthorizedAccessException();

        if (!schedules.Exists)
            throw new KeyNotFoundException("Chưa có báo cáo PDF tự động.");
        await using var session = await schedules.OpenAsync(cancellationToken);
        var metadata = session.Schedule?.Reports?.SingleOrDefault(item => item.Id == request.Id)
            ?? throw new KeyNotFoundException("Báo cáo không tồn tại hoặc đã hết thời hạn lưu.");

        return new(await files.ReadAsync(metadata.Id, cancellationToken), metadata.FileName);
    }
}
