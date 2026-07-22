namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationJoinSourceManagerTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid InvitationId = Guid.NewGuid();
    private static readonly Guid EnrollmentLinkId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Manager_dispatches_owner_checked_reads_and_deny_first_mutations()
    {
        OrganizationInvitationDto invitation = CreateInvitation();
        OrganizationEnrollmentLinkDto link = CreateEnrollmentLink();
        RecordingDispatcher dispatcher = new(request => request switch
        {
            GetOrganizationInvitationQuery => Result.Success(invitation),
            GetOrganizationEnrollmentLinkQuery => Result.Success(link),
            ListOrganizationInvitationsQuery => Result.Success(
                new OrganizationInvitationListResponse([invitation], 2, 10)),
            ListOrganizationEnrollmentLinksQuery => Result.Success(
                new OrganizationEnrollmentLinkListResponse([link], 3, 5)),
            RevokeOrganizationInvitationCommand => Result.Success(invitation with
            {
                Status = OrganizationInvitationStatus.Revoked,
                Version = invitation.Version + 1
            }),
            ChangeOrganizationEnrollmentLinkCommand => Result.Success(
                new OrganizationEnrollmentLinkMutationDto(link with
                {
                    Status = OrganizationEnrollmentLinkStatus.Disabled,
                    Version = link.Version + 1
                }, null)),
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
        });
        OrganizationJoinSourceManager manager = new(dispatcher);
        OrganizationJoinSourceListRequest listRequest = new(OrganizationId, "owner-a", 2, 10);

        OrganizationJoinSourceOperation<OrganizationInvitationDto> selectedInvitation =
            await manager.GetInvitationAsync(new(OrganizationId, InvitationId, "owner-a"));
        OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto> selectedLink =
            await manager.GetEnrollmentLinkAsync(new(OrganizationId, EnrollmentLinkId, "owner-a"));
        OrganizationJoinSourceOperation<OrganizationInvitationListResponse> invitations =
            await manager.ListInvitationsAsync(listRequest);
        OrganizationJoinSourceOperation<OrganizationEnrollmentLinkListResponse> links =
            await manager.ListEnrollmentLinksAsync(listRequest with { Page = 3, PageSize = 5 });
        OrganizationJoinSourceOperation<OrganizationInvitationDto> revoked =
            await manager.RevokeInvitationAsync(new(
                OrganizationId, InvitationId, invitation.Version, "owner-a", "owner-a"));
        OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto> disabled =
            await manager.DisableEnrollmentLinkAsync(new(
                OrganizationId, EnrollmentLinkId, link.Version, "owner-a", "owner-a"));

        Assert.Equal(InvitationId, selectedInvitation.Value!.InvitationId);
        Assert.Equal(EnrollmentLinkId, selectedLink.Value!.EnrollmentLinkId);
        Assert.True(invitations.IsSuccess);
        Assert.Equal(2, invitations.Value!.Page);
        Assert.True(links.IsSuccess);
        Assert.Equal(3, links.Value!.Page);
        Assert.Equal(OrganizationInvitationStatus.Revoked, revoked.Value!.Status);
        Assert.Equal(OrganizationEnrollmentLinkStatus.Disabled, disabled.Value!.Status);
        Assert.Collection(
            dispatcher.Requests,
            request => Assert.IsType<GetOrganizationInvitationQuery>(request),
            request => Assert.IsType<GetOrganizationEnrollmentLinkQuery>(request),
            request => Assert.IsType<ListOrganizationInvitationsQuery>(request),
            request => Assert.IsType<ListOrganizationEnrollmentLinksQuery>(request),
            request => Assert.IsType<RevokeOrganizationInvitationCommand>(request),
            request =>
            {
                ChangeOrganizationEnrollmentLinkCommand command =
                    Assert.IsType<ChangeOrganizationEnrollmentLinkCommand>(request);
                Assert.Equal(OrganizationEnrollmentLinkAction.Disable, command.Action);
                Assert.Null(command.ReplacementLifetimeHours);
            });
    }

    [Fact]
    public async Task Manager_preserves_stable_error_codes_without_returning_values()
    {
        Error denied = new("Organizations.OwnerRequired", "An active owner membership is required.");
        RecordingDispatcher dispatcher = new(request => request switch
        {
            GetOrganizationInvitationQuery =>
                Result.Failure<OrganizationInvitationDto>(denied),
            ListOrganizationInvitationsQuery =>
                Result.Failure<OrganizationInvitationListResponse>(denied),
            RevokeOrganizationInvitationCommand =>
                Result.Failure<OrganizationInvitationDto>(denied),
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
        });
        OrganizationJoinSourceManager manager = new(dispatcher);

        OrganizationJoinSourceOperation<OrganizationInvitationDto> selected =
            await manager.GetInvitationAsync(new(OrganizationId, InvitationId, "member-a"));
        OrganizationJoinSourceOperation<OrganizationInvitationListResponse> listed =
            await manager.ListInvitationsAsync(new(OrganizationId, "member-a", 1, 25));
        OrganizationJoinSourceOperation<OrganizationInvitationDto> revoked =
            await manager.RevokeInvitationAsync(new(
                OrganizationId, InvitationId, 4, "member-a", "member-a"));

        Assert.False(selected.IsSuccess);
        Assert.Equal(denied.Code, selected.ErrorCode);
        Assert.False(listed.IsSuccess);
        Assert.Null(listed.Value);
        Assert.Equal(denied.Code, listed.ErrorCode);
        Assert.False(revoked.IsSuccess);
        Assert.Null(revoked.Value);
        Assert.Equal(denied.Code, revoked.ErrorCode);
    }

    [Fact]
    public async Task Manager_rejects_null_requests_before_dispatch()
    {
        OrganizationJoinSourceManager manager = new(new RecordingDispatcher(_ =>
            throw new InvalidOperationException("The request must not be dispatched.")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.ListInvitationsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.GetInvitationAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.DisableEnrollmentLinkAsync(null!));
    }

    private static OrganizationInvitationDto CreateInvitation() =>
        new(
            InvitationId,
            OrganizationId,
            "owner-a",
            "staff@example.test",
            Now.AddDays(3),
            OrganizationInvitationStatus.Pending,
            null,
            null,
            4,
            Now,
            Now);

    private static OrganizationEnrollmentLinkDto CreateEnrollmentLink() =>
        new(
            EnrollmentLinkId,
            OrganizationId,
            "owner-a",
            Now.AddDays(1),
            20,
            2,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            OrganizationEnrollmentLinkStatus.Active,
            6,
            Now,
            Now);

    private sealed class RecordingDispatcher(Func<object, object> dispatch) : IRequestDispatcher
    {
        public List<object> Requests { get; } = [];

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default) =>
            this.Dispatch<TResponse>(command);

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            this.Dispatch<TResponse>(query);

        private Task<Result<TResponse>> Dispatch<TResponse>(object request)
        {
            this.Requests.Add(request);
            return Task.FromResult((Result<TResponse>)dispatch(request));
        }
    }
}
