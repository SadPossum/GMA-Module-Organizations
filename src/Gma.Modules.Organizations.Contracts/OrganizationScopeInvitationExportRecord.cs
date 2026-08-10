namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeInvitationExportRecord(
    Guid InvitationId,
    Guid OrganizationId,
    string InviterSubjectId,
    string? RecipientEmail,
    int TokenVersion,
    DateTimeOffset ExpiresAtUtc,
    OrganizationInvitationStatus Status,
    string? AcceptedSubjectId,
    Guid? AcceptedMembershipId,
    DateTimeOffset? AcceptedAtUtc,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc,
    Guid? ReplacesInvitationId = null,
    long? ReplacesInvitationVersion = null)
    : OrganizationScopeExportRecord;
