namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record AcceptOrganizationInvitationCommand(
    string Token,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationInvitationAcceptanceDto>;
