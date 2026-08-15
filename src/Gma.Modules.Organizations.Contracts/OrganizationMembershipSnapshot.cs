namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMembershipSnapshot(
    Guid OrganizationId,
    Guid MembershipId,
    OrganizationStatus OrganizationStatus,
    OrganizationScopeStatus ScopeStatus,
    long ScopeRevision,
    OrganizationMembershipRole Role,
    OrganizationMembershipStatus MembershipStatus,
    long MembershipVersion);
