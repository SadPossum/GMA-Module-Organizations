namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationRecipientVerificationRequest(
    Guid OrganizationId,
    Guid InvitationId,
    string SubjectId,
    string RecipientEmail);
