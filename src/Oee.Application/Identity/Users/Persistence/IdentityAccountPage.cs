namespace Oee.Application.Identity;

public sealed record IdentityAccountPage(
    ManagedUserAccount[] Accounts,
    IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> RolesByUser,
    int TotalCount);
