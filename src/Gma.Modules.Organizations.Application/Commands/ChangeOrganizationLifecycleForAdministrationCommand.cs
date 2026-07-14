namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record ChangeOrganizationLifecycleForAdministrationCommand(
    Guid OrganizationId,
    OrganizationLifecycleAction Action,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<OrganizationDto>;
