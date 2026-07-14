namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record RevokeOrganizationInvitationCommand(
    Guid OrganizationId,
    Guid InvitationId,
    long ExpectedVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationInvitationDto>;
