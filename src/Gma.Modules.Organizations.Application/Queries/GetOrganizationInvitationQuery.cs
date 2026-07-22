namespace Gma.Modules.Organizations.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record GetOrganizationInvitationQuery(
    Guid OrganizationId,
    Guid InvitationId,
    string SubjectId) : IQuery<OrganizationInvitationDto>;
