namespace Gma.Modules.Organizations.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;

internal sealed class OrganizationJoinSourceManager(IRequestDispatcher dispatcher)
    : IOrganizationJoinSourceManager
{
    public async Task<OrganizationJoinSourceOperation<OrganizationInvitationListResponse>> ListInvitationsAsync(
        OrganizationJoinSourceListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<OrganizationInvitationListResponse> result = await dispatcher.QueryAsync(
                new ListOrganizationInvitationsQuery(
                    request.OrganizationId,
                    request.SubjectId,
                    request.Page,
                    request.PageSize),
                cancellationToken)
            .ConfigureAwait(false);
        return Complete(result);
    }

    public async Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkListResponse>> ListEnrollmentLinksAsync(
        OrganizationJoinSourceListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<OrganizationEnrollmentLinkListResponse> result = await dispatcher.QueryAsync(
                new ListOrganizationEnrollmentLinksQuery(
                    request.OrganizationId,
                    request.SubjectId,
                    request.Page,
                    request.PageSize),
                cancellationToken)
            .ConfigureAwait(false);
        return Complete(result);
    }

    public async Task<OrganizationJoinSourceOperation<OrganizationInvitationDto>> RevokeInvitationAsync(
        OrganizationInvitationRevocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<OrganizationInvitationDto> result = await dispatcher.SendAsync(
                new RevokeOrganizationInvitationCommand(
                    request.OrganizationId,
                    request.InvitationId,
                    request.ExpectedVersion,
                    request.SubjectId,
                    request.ActorId),
                cancellationToken)
            .ConfigureAwait(false);
        return Complete(result);
    }

    public async Task<OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto>> DisableEnrollmentLinkAsync(
        OrganizationEnrollmentLinkDisableRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<OrganizationEnrollmentLinkMutationDto> result = await dispatcher.SendAsync(
                new ChangeOrganizationEnrollmentLinkCommand(
                    request.OrganizationId,
                    request.EnrollmentLinkId,
                    OrganizationEnrollmentLinkAction.Disable,
                    request.ExpectedVersion,
                    ReplacementLifetimeHours: null,
                    request.SubjectId,
                    request.ActorId),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsFailure
            ? Failure<OrganizationEnrollmentLinkDto>(result.Error.Code)
            : Success(result.Value.EnrollmentLink);
    }

    private static OrganizationJoinSourceOperation<TValue> Complete<TValue>(Result<TValue> result)
        where TValue : class => result.IsFailure
            ? Failure<TValue>(result.Error.Code)
            : Success(result.Value);

    private static OrganizationJoinSourceOperation<TValue> Success<TValue>(TValue value)
        where TValue : class => new(value, null);

    private static OrganizationJoinSourceOperation<TValue> Failure<TValue>(string errorCode)
        where TValue : class => new(null, errorCode);
}
