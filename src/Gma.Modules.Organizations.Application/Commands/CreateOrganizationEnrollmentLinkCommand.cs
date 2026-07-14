namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record CreateOrganizationEnrollmentLinkCommand(
    Guid OrganizationId,
    int? LifetimeHours,
    int MaximumClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationEnrollmentLinkIssuedDto>;
