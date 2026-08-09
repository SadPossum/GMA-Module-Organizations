namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationMembershipChangePolicyTests
{
    private static readonly OrganizationMembershipChangePolicyRequest Request = new(
        Guid.NewGuid(),
        "owner",
        "member-a",
        OrganizationMembershipRole.Member,
        OrganizationMembershipStatus.Active,
        OrganizationMembershipStatus.Suspended);

    [Fact]
    public async Task No_product_policy_preserves_standalone_behavior()
    {
        OrganizationMembershipChangePolicy policy = new([]);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(OrganizationMembershipChangePolicyDecision.Denied, "Organizations.MembershipChangeRejected")]
    [InlineData(OrganizationMembershipChangePolicyDecision.Unavailable, "Organizations.MembershipChangeUnavailable")]
    [InlineData(OrganizationMembershipChangePolicyDecision.Unknown, "Organizations.MembershipChangeUnavailable")]
    [InlineData((OrganizationMembershipChangePolicyDecision)999, "Organizations.MembershipChangeUnavailable")]
    public async Task Product_decisions_have_stable_fail_closed_results(
        OrganizationMembershipChangePolicyDecision decision,
        string expectedErrorCode)
    {
        OrganizationMembershipChangePolicy policy = new([new FixedPolicy(decision)]);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(expectedErrorCode, result.Error.Code);
    }

    [Fact]
    public async Task Every_product_policy_must_allow_the_change()
    {
        RecordingPolicy first = new(OrganizationMembershipChangePolicyDecision.Allowed);
        RecordingPolicy second = new(OrganizationMembershipChangePolicyDecision.Allowed);
        OrganizationMembershipChangePolicy policy = new([first, second]);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, first.InvocationCount);
        Assert.Equal(1, second.InvocationCount);
    }

    [Fact]
    public async Task Product_policy_evaluation_stops_after_the_first_failure()
    {
        RecordingPolicy allowed = new(OrganizationMembershipChangePolicyDecision.Allowed);
        RecordingPolicy unavailable = new(OrganizationMembershipChangePolicyDecision.Unavailable);
        RecordingPolicy trailing = new(OrganizationMembershipChangePolicyDecision.Allowed);
        OrganizationMembershipChangePolicy policy = new([allowed, unavailable, trailing]);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.MembershipChangeUnavailable, result.Error);
        Assert.Equal(1, allowed.InvocationCount);
        Assert.Equal(1, unavailable.InvocationCount);
        Assert.Equal(0, trailing.InvocationCount);
    }

    [Fact]
    public async Task Product_policy_exception_is_temporarily_unavailable()
    {
        OrganizationMembershipChangePolicy policy = new([new ThrowingPolicy()]);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.MembershipChangeUnavailable, result.Error);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_to_unavailability()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OrganizationMembershipChangePolicy policy = new([new ThrowingPolicy()]);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await policy.AuthorizeAsync(Request, cancellation.Token));
    }

    private sealed class FixedPolicy(OrganizationMembershipChangePolicyDecision decision)
        : IOrganizationMembershipChangePolicy
    {
        public ValueTask<OrganizationMembershipChangePolicyDecision> EvaluateAsync(
            OrganizationMembershipChangePolicyRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(decision);
    }

    private sealed class RecordingPolicy(OrganizationMembershipChangePolicyDecision decision)
        : IOrganizationMembershipChangePolicy
    {
        public int InvocationCount { get; private set; }

        public ValueTask<OrganizationMembershipChangePolicyDecision> EvaluateAsync(
            OrganizationMembershipChangePolicyRequest request,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class ThrowingPolicy : IOrganizationMembershipChangePolicy
    {
        public ValueTask<OrganizationMembershipChangePolicyDecision> EvaluateAsync(
            OrganizationMembershipChangePolicyRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Policy unavailable.");
        }
    }
}
