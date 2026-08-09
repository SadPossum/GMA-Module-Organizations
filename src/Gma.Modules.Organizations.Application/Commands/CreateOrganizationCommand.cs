namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record CreateOrganizationCommand(
    Guid OperationId,
    string Name,
    string Slug,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationMembershipSummaryDto>;
