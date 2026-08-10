namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeDestroyRequest(
    Guid OperationId,
    Guid OrganizationId,
    long ExpectedRevision,
    int BatchSize);
