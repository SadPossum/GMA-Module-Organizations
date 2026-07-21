namespace Gma.Modules.Organizations.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;

internal sealed class OrganizationJoinSourceIssuer(IRequestDispatcher dispatcher)
    : IOrganizationJoinSourceIssuer
{
    public async Task<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> IssueInvitationAsync(
        OrganizationInvitationIssuanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> result =
            await dispatcher.SendAsync(
                    new IssueOrganizationInvitationCommand(request),
                    cancellationToken)
                .ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value
            : Failure<OrganizationInvitationDto>(result.Error.Code);
    }

    public async Task<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> IssueEnrollmentLinkAsync(
        OrganizationEnrollmentLinkIssuanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> result =
            await dispatcher.SendAsync(
                    new IssueOrganizationEnrollmentLinkCommand(request),
                    cancellationToken)
                .ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value
            : Failure<OrganizationEnrollmentLinkDto>(result.Error.Code);
    }

    private static OrganizationJoinSourceIssuance<TSource> Failure<TSource>(string errorCode)
        where TSource : class => new(
            null,
            OrganizationJoinSourceIssuanceOutcome.Unknown,
            null,
            errorCode);
}
