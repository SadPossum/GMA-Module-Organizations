namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record EnsureOrganizationMembershipStateCommand(
    Guid OrganizationId,
    string SubjectId,
    OrganizationMembershipStatus DesiredStatus,
    string ActorId) : ITransactionalCommand<OrganizationMembershipLifecycleResult>;
