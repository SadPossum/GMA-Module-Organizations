namespace Gma.Modules.Organizations.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record GetOrganizationEnrollmentLinkQuery(
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    string SubjectId) : IQuery<OrganizationEnrollmentLinkDto>;
