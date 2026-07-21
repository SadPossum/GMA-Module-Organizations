namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record IssueOrganizationEnrollmentLinkCommand(
    OrganizationEnrollmentLinkIssuanceRequest Request)
    : ITransactionalCommand<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>;
