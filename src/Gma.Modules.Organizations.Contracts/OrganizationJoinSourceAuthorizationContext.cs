namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationJoinSourceAuthorizationContext(
    OrganizationJoinSourceAuthorizationOperation Operation,
    Guid OrganizationId,
    string SubjectId,
    Guid? SourceId = null,
    Guid? ClaimId = null);
