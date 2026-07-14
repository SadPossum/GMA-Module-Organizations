namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public enum OrganizationLifecycleAction
{
    Unknown = 0,
    Suspend = 1,
    Reactivate = 2,
    Archive = 3
}

public sealed record ChangeOrganizationLifecycleCommand(
    Guid OrganizationId,
    OrganizationLifecycleAction Action,
    long ExpectedVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationDto>;
