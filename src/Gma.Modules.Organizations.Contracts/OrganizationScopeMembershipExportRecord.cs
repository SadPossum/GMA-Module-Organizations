namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeMembershipExportRecord(
    Guid MembershipId,
    Guid OrganizationId,
    string SubjectId,
    OrganizationMembershipRole Role,
    OrganizationMembershipStatus Status,
    long Version,
    string CreatedBy,
    DateTimeOffset JoinedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;
