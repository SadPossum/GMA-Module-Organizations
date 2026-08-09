namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationIssuanceRequest(
    Guid SourceId,
    Guid OrganizationId,
    string? RecipientEmail,
    int? LifetimeHours,
    string SubjectId,
    string ActorId);
