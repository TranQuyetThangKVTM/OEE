using Oee.Application.Contracts.Operations;

namespace Oee.Tests.UseCases;
/// <summary>Tạo phiên bản mới cho cấu hình Standard/Target có khoảng hiệu lực.</summary>
public interface IEffectiveConfigurationScenario
{
    /// <summary>Thêm phiên bản tốc độ chuẩn sau khi kiểm tra không chồng hiệu lực.</summary>
    Task<Guid> AddStandardVersionAsync(EffectiveStandardRequest request, CancellationToken cancellationToken);
    /// <summary>Thêm phiên bản target OEE sau khi kiểm tra scope và khoảng hiệu lực.</summary>
    Task<Guid> AddTargetVersionAsync(EffectiveTargetRequest request, CancellationToken cancellationToken);
}
