namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMembershipDto(
    Guid MembershipId,
    Guid OrganizationId,
    string SubjectId,
    OrganizationMembershipRole Role,
    OrganizationMembershipStatus Status,
    long Version,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset LastChangedAtUtc);
