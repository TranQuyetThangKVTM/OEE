
namespace Oee.Infrastructure.BackgroundServices;

// Signature gồm RowVersion của connection, các mapping và tag; chỉ restart khi cấu hình đổi.
internal sealed record OpcUaConnectionRunner(string Signature, CancellationTokenSource Stop, Task Task);
