namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record RotateOrganizationEnrollmentLinkCommand(
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    Guid ReplacementSourceId,
    long ExpectedVersion,
    int? ReplacementLifetimeHours,
    string SubjectId,
    string ActorId) : ITransactionalCommand<
        OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>;
