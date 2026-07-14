namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationDto(
    Guid InvitationId,
    Guid OrganizationId,
    string InviterSubjectId,
    string? RecipientEmail,
    DateTimeOffset ExpiresAtUtc,
    OrganizationInvitationStatus Status,
    string? AcceptedSubjectId,
    Guid? AcceptedMembershipId,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);
