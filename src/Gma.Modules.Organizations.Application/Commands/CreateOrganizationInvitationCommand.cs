namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record CreateOrganizationInvitationCommand(
    Guid OrganizationId,
    string? RecipientEmail,
    int? LifetimeHours,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationInvitationIssuedDto>;
