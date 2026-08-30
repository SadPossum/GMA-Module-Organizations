namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMembershipSnapshotDto(
    Guid OrganizationId,
    OrganizationStatus OrganizationStatus,
    OrganizationMembershipDto? Membership);
