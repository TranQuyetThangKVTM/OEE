using Oee.Application.PlcIntegration;
using Opc.Ua.Client;

namespace Oee.Infrastructure.Integrations.OpcUa;

/// <summary>Handle vòng đời subscription; Dispose xóa subscription trên server trước khi giải phóng.</summary>
internal sealed class OpcUaSdkSubscription(Subscription subscription, CancellationTokenSource stop) : IOpcUaSubscription
{
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await subscription.DeleteAsync(true, timeout.Token).WaitAsync(timeout.Token); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        finally { subscription.Dispose(); stop.Dispose(); }
    }
}
