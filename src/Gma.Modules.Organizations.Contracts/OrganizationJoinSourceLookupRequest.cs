namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinSourceLookupRequest(
    Guid OrganizationId,
    Guid SourceId,
    string SubjectId);
