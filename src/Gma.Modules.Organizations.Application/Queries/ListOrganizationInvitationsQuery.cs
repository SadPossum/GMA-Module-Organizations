namespace Gma.Modules.Organizations.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record ListOrganizationInvitationsQuery(
    Guid OrganizationId,
    string SubjectId,
    int Page,
    int PageSize) : IQuery<OrganizationInvitationListResponse>;
