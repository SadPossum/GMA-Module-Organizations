namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationJoinTokenInspectorTests
{
    [Fact]
    public async Task Invitation_inspection_returns_the_query_preview_without_exposing_dispatcher_details()
    {
        OrganizationInvitationPreviewDto preview = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Workspace",
            "workspace",
            false,
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero),
            OrganizationInvitationStatus.Pending);
        StubDispatcher dispatcher = new() { Response = preview };
        using ServiceProvider provider = CreateProvider(dispatcher);
        IOrganizationJoinTokenInspector inspector = provider
            .GetRequiredService<IOrganizationJoinTokenInspector>();

        OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto> result =
            await inspector.InspectInvitationAsync("secret-token", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(preview, result.Preview);
        Assert.Null(result.ErrorCode);
        PreviewOrganizationInvitationQuery query = Assert.IsType<PreviewOrganizationInvitationQuery>(
            dispatcher.LastQuery);
        Assert.Equal("secret-token", query.Token);
    }

    [Fact]
    public async Task Enrollment_inspection_preserves_a_stable_failure_code()
    {
        StubDispatcher dispatcher = new()
        {
            Error = new Error("Organizations.EnrollmentLinkInvalid", "Invalid enrollment link.")
        };
        using ServiceProvider provider = CreateProvider(dispatcher);
        IOrganizationJoinTokenInspector inspector = provider
            .GetRequiredService<IOrganizationJoinTokenInspector>();

        OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto> result =
            await inspector.InspectEnrollmentAsync("invalid-token", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Preview);
        Assert.Equal("Organizations.EnrollmentLinkInvalid", result.ErrorCode);
        Assert.IsType<PreviewOrganizationEnrollmentLinkQuery>(dispatcher.LastQuery);
    }

    private static ServiceProvider CreateProvider(IRequestDispatcher dispatcher)
    {
        ServiceCollection services = new();
        services.AddSingleton(dispatcher);
        services.AddOrganizationsApplication(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private sealed class StubDispatcher : IRequestDispatcher
    {
        public object? Response { get; init; }
        public Error Error { get; init; } = Error.None;
        public object? LastQuery { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default)
        {
            this.LastQuery = query;
            return Task.FromResult(this.Response is TResponse response
                ? Result.Success(response)
                : Result.Failure<TResponse>(this.Error));
        }
    }
}
