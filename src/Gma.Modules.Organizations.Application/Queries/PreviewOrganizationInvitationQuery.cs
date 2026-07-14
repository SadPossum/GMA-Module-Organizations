namespace Gma.Modules.Organizations.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record PreviewOrganizationInvitationQuery(string Token)
    : IQuery<OrganizationInvitationPreviewDto>;
