namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeDestroyResult(
    OrganizationScopeDestroyStatus Status,
    OrganizationScopeDestroyProgress? Progress,
    OrganizationScopeDestroyReceipt? Receipt);
