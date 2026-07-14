namespace Gma.Modules.Organizations.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record GetOrganizationQuery(Guid OrganizationId, string SubjectId)
    : IQuery<OrganizationMembershipSummaryDto>;
