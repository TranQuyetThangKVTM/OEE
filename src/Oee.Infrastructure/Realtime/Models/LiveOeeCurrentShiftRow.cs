
namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeeCurrentShiftRow(string Code, string Name, DateTimeOffset Start, DateTimeOffset End, decimal Progress);
