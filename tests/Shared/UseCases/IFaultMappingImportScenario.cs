using Oee.Application.Contracts.Reporting;

namespace Oee.Tests.UseCases;
/// <summary>Import XLSX fault mapping theo quy trình template → dry-run → import.</summary>
public interface IFaultMappingImportScenario
{
    /// <summary>Tạo template versioned không chứa dữ liệu nhạy cảm.</summary>
    byte[] CreateTemplate();
    /// <summary>Đọc và validate file nhưng chưa ghi SQL.</summary>
    Task<FaultMappingImportPreview> PreviewAsync(Stream xlsx, long length, CancellationToken cancellationToken);
    /// <summary>Validate lại và ghi các dòng hợp lệ theo transaction/batch.</summary>
    Task<FaultMappingImportResult> ImportAsync(Stream xlsx, long length, CancellationToken cancellationToken);
}
