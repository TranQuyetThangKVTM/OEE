using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Downtime.Reasons.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác DowntimeReason.</summary>
internal sealed class DowntimeReasonContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal Task EnsureUniqueReasonCodeAsync(
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnyDowntimeReasonsAsync(x => x.Code == code && x.Id != id, cancellationToken),
            "Downtime-reason code already exists.");
}
