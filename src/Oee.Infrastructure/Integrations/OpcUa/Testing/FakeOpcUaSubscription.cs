using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.Integrations.OpcUa;

internal sealed class FakeOpcUaSubscription(Action dispose) : IOpcUaSubscription { public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; } }
