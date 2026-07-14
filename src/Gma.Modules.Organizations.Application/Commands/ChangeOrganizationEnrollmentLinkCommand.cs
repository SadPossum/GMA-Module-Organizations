namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public enum OrganizationEnrollmentLinkAction
{
    Unknown = 0,
    Disable = 1,
    Rotate = 2
}

public sealed record ChangeOrganizationEnrollmentLinkCommand(
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    OrganizationEnrollmentLinkAction Action,
    long ExpectedVersion,
    int? ReplacementLifetimeHours,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationEnrollmentLinkMutationDto>;
