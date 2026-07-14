namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public enum OrganizationJoinRequestDecision
{
    Unknown = 0,
    Approve = 1,
    Reject = 2
}

public sealed record ResolveOrganizationJoinRequestCommand(
    Guid OrganizationId,
    Guid ClaimId,
    OrganizationJoinRequestDecision Decision,
    long ExpectedClaimVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationEnrollmentOutcomeDto>;
