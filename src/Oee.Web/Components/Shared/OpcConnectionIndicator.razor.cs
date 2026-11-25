using Microsoft.AspNetCore.Components;

#pragma warning disable CA1716
namespace Oee.Web.Components.Shared;
#pragma warning restore CA1716

public partial class OpcConnectionIndicator
{
    private Oee.Application.PlcIntegration.OpcUiStatus? _status;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _stopTokenSource;

    [Parameter]
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(15);

    private bool _loadFailed;

    private string ConnectionState => _loadFailed ? "offline" : _status?.WorstState switch
    {
        Oee.Domain.Common.ConnectivityStateEnum.Connected => "connected",
        Oee.Domain.Common.ConnectivityStateEnum.NotConfigured => "unconfigured",
        null => "checking",
        _ => "offline"
    };

    private string StatusText => _loadFailed ? "Chưa đọc được trạng thái" : _status?.WorstState switch
    {
        Oee.Domain.Common.ConnectivityStateEnum.Connected => "Đã kết nối",
        Oee.Domain.Common.ConnectivityStateEnum.Disconnected => "Mất kết nối",
        Oee.Domain.Common.ConnectivityStateEnum.NoData => "Không có dữ liệu mới",
        Oee.Domain.Common.ConnectivityStateEnum.BadTag => "Lỗi tag",
        Oee.Domain.Common.ConnectivityStateEnum.NotConfigured => "Chưa cấu hình",
        _ => "Đang kiểm tra"
    };

    private string StatusDetails => _loadFailed
        ? "Không thể đọc trạng thái OPC. Mở giám sát tag để kiểm tra; dữ liệu xác nhận gần nhất được giữ nguyên."
        : _status is null ? "Đang kiểm tra kết nối OPC UA."
        : $"OPC UA · {StatusText}. Đã cấu hình: {_status.Configured}; kết nối: {_status.Connected}; cần kiểm tra: {_status.Unhealthy}. Kiểm tra lúc {_status.CheckedAt.ToLocalTime():HH:mm:ss}.";

    protected override async Task OnInitializedAsync()
    {
        // Tải ngay một lần rồi poll ở một component global, tránh mỗi page tự query database dày đặc.
        _stopTokenSource = new CancellationTokenSource();
        await RefreshAsync(_stopTokenSource.Token);

        _timer = new PeriodicTimer(RefreshInterval);
        _ = PollAsync(_stopTokenSource.Token);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            _status = await StatusService.GetAsync(cancellationToken);
            _loadFailed = false;
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            // Không suy diễn số thiết bị mất kết nối khi chính truy vấn trạng thái thất bại.
            _loadFailed = true;
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_timer is not null && await _timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync(cancellationToken);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Dispose component là luồng bình thường, không phải lỗi cần báo.
        }
    }

    public ValueTask DisposeAsync()
    {
        // Dừng timer/poll khi layout circuit bị dispose để tránh callback vào component đã đóng.
        _stopTokenSource?.Cancel();
        _timer?.Dispose();
        _stopTokenSource?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
