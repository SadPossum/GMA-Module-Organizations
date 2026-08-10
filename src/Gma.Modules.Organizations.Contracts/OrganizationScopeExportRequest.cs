namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeExportRequest(
    Guid OrganizationId,
    long ExpectedRevision,
    OrganizationScopeExportStore Store,
    string? AfterCursor,
    int PageSize);
