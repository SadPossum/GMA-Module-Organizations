namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record IssueOrganizationInvitationCommand(
    OrganizationInvitationIssuanceRequest Request)
    : ITransactionalCommand<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>;
