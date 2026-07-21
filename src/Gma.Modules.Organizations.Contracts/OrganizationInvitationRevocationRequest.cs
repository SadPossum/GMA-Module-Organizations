namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationInvitationRevocationRequest(
    Guid OrganizationId,
    Guid InvitationId,
    long ExpectedVersion,
    string SubjectId,
    string ActorId);
