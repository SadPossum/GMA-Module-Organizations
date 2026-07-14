namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record ReissueOrganizationInvitationCommand(
    Guid OrganizationId,
    Guid InvitationId,
    long ExpectedVersion,
    int? LifetimeHours,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationInvitationIssuedDto>;
