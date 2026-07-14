namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record UpdateOrganizationCommand(
    Guid OrganizationId,
    string Name,
    string Slug,
    long ExpectedVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationDto>;
