namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationJoinAdmissionPolicyTests
{
    private static readonly OrganizationJoinAdmissionContext Context = new(
        OrganizationJoinAdmissionOperation.AcceptInvitation,
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        "applicant",
        "applicant",
        null);

    [Fact]
    public async Task No_product_policy_preserves_standalone_admission()
    {
        OrganizationJoinAdmissionPolicy policy = new([]);

        Result result = await policy.AuthorizeAsync(Context, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(OrganizationJoinAdmissionDecision.Denied, "Organizations.JoinAdmissionRejected")]
    [InlineData(OrganizationJoinAdmissionDecision.Unavailable, "Organizations.JoinAdmissionUnavailable")]
    [InlineData(OrganizationJoinAdmissionDecision.Unknown, "Organizations.JoinAdmissionUnavailable")]
    [InlineData((OrganizationJoinAdmissionDecision)999, "Organizations.JoinAdmissionUnavailable")]
    public async Task Product_decisions_have_stable_fail_closed_results(
        OrganizationJoinAdmissionDecision decision,
        string expectedErrorCode)
    {
        OrganizationJoinAdmissionPolicy policy = new([new FixedPolicy(decision)]);

        Result result = await policy.AuthorizeAsync(Context, CancellationToken.None);

        Assert.Equal(expectedErrorCode, result.Error.Code);
    }

    [Fact]
    public async Task Every_product_policy_must_allow_admission()
    {
        RecordingPolicy first = new(OrganizationJoinAdmissionDecision.Allowed);
        RecordingPolicy second = new(OrganizationJoinAdmissionDecision.Allowed);
        OrganizationJoinAdmissionPolicy policy = new([first, second]);

        Result result = await policy.AuthorizeAsync(Context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, first.InvocationCount);
        Assert.Equal(1, second.InvocationCount);
    }

    [Fact]
    public async Task Product_policy_evaluation_stops_after_the_first_failure()
    {
        RecordingPolicy allowed = new(OrganizationJoinAdmissionDecision.Allowed);
        RecordingPolicy unavailable = new(OrganizationJoinAdmissionDecision.Unavailable);
        RecordingPolicy trailing = new(OrganizationJoinAdmissionDecision.Allowed);
        OrganizationJoinAdmissionPolicy policy = new([allowed, unavailable, trailing]);

        Result result = await policy.AuthorizeAsync(Context, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.JoinAdmissionUnavailable, result.Error);
        Assert.Equal(1, allowed.InvocationCount);
        Assert.Equal(1, unavailable.InvocationCount);
        Assert.Equal(0, trailing.InvocationCount);
    }

    [Fact]
    public async Task Product_policy_exception_is_temporarily_unavailable()
    {
        OrganizationJoinAdmissionPolicy policy = new([new ThrowingPolicy()]);

        Result result = await policy.AuthorizeAsync(Context, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.JoinAdmissionUnavailable, result.Error);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_to_unavailability()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OrganizationJoinAdmissionPolicy policy = new([new ThrowingPolicy()]);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await policy.AuthorizeAsync(Context, cancellation.Token));
    }

    private sealed class FixedPolicy(OrganizationJoinAdmissionDecision decision)
        : IOrganizationJoinAdmissionPolicy
    {
        public ValueTask<OrganizationJoinAdmissionDecision> EvaluateAsync(
            OrganizationJoinAdmissionContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(decision);
    }

    private sealed class RecordingPolicy(OrganizationJoinAdmissionDecision decision)
        : IOrganizationJoinAdmissionPolicy
    {
        public int InvocationCount { get; private set; }

        public ValueTask<OrganizationJoinAdmissionDecision> EvaluateAsync(
            OrganizationJoinAdmissionContext context,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class ThrowingPolicy : IOrganizationJoinAdmissionPolicy
    {
        public ValueTask<OrganizationJoinAdmissionDecision> EvaluateAsync(
            OrganizationJoinAdmissionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Policy unavailable.");
        }
    }
}
