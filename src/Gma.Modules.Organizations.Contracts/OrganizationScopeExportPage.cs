namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeExportPage(
    OrganizationScopeExportStatus Status,
    long ScopeRevision,
    OrganizationScopeExportStore Store,
    IReadOnlyList<OrganizationScopeExportRecord> Records,
    string? NextCursor,
    bool HasMore);
