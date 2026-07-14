namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record TransferOrganizationOwnershipCommand(
    Guid OrganizationId,
    string TargetSubjectId,
    long ExpectedOrganizationVersion,
    long ExpectedCurrentOwnerVersion,
    long ExpectedTargetVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationMembershipDto>;
