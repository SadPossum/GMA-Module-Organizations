namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record WithdrawOrganizationJoinRequestCommand(
    Guid OrganizationId,
    Guid ClaimId,
    long ExpectedClaimVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationEnrollmentOutcomeDto>;
