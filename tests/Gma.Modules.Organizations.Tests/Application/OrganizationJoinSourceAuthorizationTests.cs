namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationJoinSourceAuthorizationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Owner_keeps_authority_without_consulting_extension_policies()
    {
        TestOrganizationRepository repository = CreateRepository();
        RecordingPolicy policy = new(
            OrganizationJoinSourceAuthorizationDecision.Denied);
        OrganizationJoinSourceAuthorization authorization = Create(
            repository,
            policy);

        var result = await authorization.AuthorizeAsync(
            Context(repository, "owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Empty(policy.Contexts);
    }

    [Fact]
    public async Task Active_member_requires_one_explicit_allow_decision()
    {
        TestOrganizationRepository repository = CreateRepositoryWithMember();
        OrganizationJoinSourceAuthorization authorization = Create(repository);

        var result = await authorization.AuthorizeAsync(
            Context(repository, "manager"),
            CancellationToken.None);

        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceManagementRequired,
            result.Error);
    }

    [Fact]
    public async Task Explicit_allow_authorizes_the_exact_context()
    {
        TestOrganizationRepository repository = CreateRepositoryWithMember();
        RecordingPolicy policy = new(
            OrganizationJoinSourceAuthorizationDecision.Allowed);
        OrganizationJoinSourceAuthorization authorization = Create(
            repository,
            policy);
        OrganizationJoinSourceAuthorizationContext context =
            Context(repository, "manager");

        var result = await authorization.AuthorizeAsync(
            context,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(context, Assert.Single(policy.Contexts));
    }

    [Fact]
    public async Task Explicit_deny_overrides_an_allow()
    {
        TestOrganizationRepository repository = CreateRepositoryWithMember();
        OrganizationJoinSourceAuthorization authorization = Create(
            repository,
            new RecordingPolicy(
                OrganizationJoinSourceAuthorizationDecision.Allowed),
            new RecordingPolicy(
                OrganizationJoinSourceAuthorizationDecision.Denied));

        var result = await authorization.AuthorizeAsync(
            Context(repository, "manager"),
            CancellationToken.None);

        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceManagementRequired,
            result.Error);
    }

    [Theory]
    [InlineData(OrganizationJoinSourceAuthorizationDecision.Unknown)]
    [InlineData(OrganizationJoinSourceAuthorizationDecision.Unavailable)]
    public async Task Unknown_or_unavailable_policy_fails_closed(
        OrganizationJoinSourceAuthorizationDecision decision)
    {
        TestOrganizationRepository repository = CreateRepositoryWithMember();
        OrganizationJoinSourceAuthorization authorization = Create(
            repository,
            new RecordingPolicy(decision));

        var result = await authorization.AuthorizeAsync(
            Context(repository, "manager"),
            CancellationToken.None);

        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceAuthorizationUnavailable,
            result.Error);
    }

    [Fact]
    public async Task Policy_exception_fails_closed()
    {
        TestOrganizationRepository repository = CreateRepositoryWithMember();
        OrganizationJoinSourceAuthorization authorization = Create(
            repository,
            new ThrowingPolicy());

        var result = await authorization.AuthorizeAsync(
            Context(repository, "manager"),
            CancellationToken.None);

        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceAuthorizationUnavailable,
            result.Error);
    }

    [Fact]
    public async Task Missing_membership_is_denied_before_policy_evaluation()
    {
        TestOrganizationRepository repository = CreateRepository();
        RecordingPolicy policy = new(
            OrganizationJoinSourceAuthorizationDecision.Allowed);
        OrganizationJoinSourceAuthorization authorization = Create(
            repository,
            policy);

        var result = await authorization.AuthorizeAsync(
            Context(repository, "outsider"),
            CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.MembershipRequired, result.Error);
        Assert.Empty(policy.Contexts);
    }

    private static OrganizationJoinSourceAuthorizationContext Context(
        TestOrganizationRepository repository,
        string subjectId) => new(
        OrganizationJoinSourceAuthorizationOperation.IssueInvitation,
        Assert.Single(repository.Organizations).Id,
        subjectId,
        Guid.NewGuid());

    private static OrganizationJoinSourceAuthorization Create(
        TestOrganizationRepository repository,
        params IOrganizationJoinSourceAuthorizationPolicy[] policies) => new(
        repository,
        policies,
        NullLogger<OrganizationJoinSourceAuthorization>.Instance);

    private static TestOrganizationRepository CreateRepositoryWithMember()
    {
        TestOrganizationRepository repository = CreateRepository();
        Organization organization = Assert.Single(repository.Organizations);
        repository.Memberships.Add(OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "manager",
            DomainMembershipRole.Member,
            "owner",
            Guid.NewGuid(),
            Now).Value);
        return repository;
    }

    private static TestOrganizationRepository CreateRepository()
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(),
            "Harbor House",
            "harbor-house",
            "owner",
            Guid.NewGuid(),
            Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(),
            organization.Id,
            "owner",
            DomainMembershipRole.Owner,
            "owner",
            Guid.NewGuid(),
            Now).Value;
        return new TestOrganizationRepository(organization, owner);
    }

    private sealed class RecordingPolicy(
        OrganizationJoinSourceAuthorizationDecision decision)
        : IOrganizationJoinSourceAuthorizationPolicy
    {
        public List<OrganizationJoinSourceAuthorizationContext> Contexts { get; } = [];

        public ValueTask<OrganizationJoinSourceAuthorizationDecision> EvaluateAsync(
            OrganizationJoinSourceAuthorizationContext context,
            CancellationToken cancellationToken = default)
        {
            this.Contexts.Add(context);
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class ThrowingPolicy
        : IOrganizationJoinSourceAuthorizationPolicy
    {
        public ValueTask<OrganizationJoinSourceAuthorizationDecision> EvaluateAsync(
            OrganizationJoinSourceAuthorizationContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unavailable");
    }
}
