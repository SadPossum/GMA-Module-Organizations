namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record DisableOrganizationEnrollmentLinkCommand(
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    long ExpectedVersion,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationEnrollmentLinkDto>;
