namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinSourceListRequest(
    Guid OrganizationId,
    string SubjectId,
    int Page,
    int PageSize);
