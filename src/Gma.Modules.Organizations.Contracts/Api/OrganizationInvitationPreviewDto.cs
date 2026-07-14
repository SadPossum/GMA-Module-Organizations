namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationPreviewDto(
    Guid InvitationId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    bool RecipientBound,
    DateTimeOffset ExpiresAtUtc,
    OrganizationInvitationStatus Status);
