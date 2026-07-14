namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record EnsureOrganizationOwnerForAdministrationCommand(
    Guid OrganizationId,
    string TargetSubjectId,
    long ExpectedOrganizationVersion,
    long? ExpectedMembershipVersion,
    string ActorId) : ITransactionalCommand<OrganizationMembershipSummaryDto>;
