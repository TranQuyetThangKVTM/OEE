namespace Oee.Infrastructure.Integrations.OpcUa;

/// <summary>Source gốc yêu cầu hai keep-alive Bad liên tiếp trước khi ép tạo lại session.</summary>
internal sealed class OpcUaKeepAliveState
{
    private int failures;
    private int reconnectRequested;

    internal bool ReconnectRequested => Volatile.Read(ref reconnectRequested) != 0;

    internal void Observe(bool bad)
    {
        if (!bad) Interlocked.Exchange(ref failures, 0);
        else if (Interlocked.Increment(ref failures) >= 2)
            Interlocked.Exchange(ref reconnectRequested, 1);
    }
}
