namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationCreationAdmissionPolicyTests
{
    private static readonly OrganizationCreationAdmissionRequest Request = new(
        Guid.NewGuid(),
        "Harbor House",
        "harbor-house",
        "subject-a",
        "user:subject-a");

    [Fact]
    public async Task Module_option_denies_before_product_policies()
    {
        RecordingPolicy product = new(OrganizationCreationAdmissionDecision.Allowed);
        OrganizationCreationAdmissionPolicy policy = Create(enabled: false, product);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.SelfServiceCreationDisabled, result.Error);
        Assert.Equal(0, product.InvocationCount);
    }

    [Fact]
    public async Task Enabled_standalone_module_allows_without_product_policies()
    {
        OrganizationCreationAdmissionPolicy policy = Create(enabled: true);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(OrganizationCreationAdmissionDecision.Denied, "Organizations.CreationRejected")]
    [InlineData(OrganizationCreationAdmissionDecision.SubjectVerificationRequired, "Organizations.SubjectVerificationRequired")]
    [InlineData(OrganizationCreationAdmissionDecision.Unavailable, "Organizations.CreationAdmissionUnavailable")]
    [InlineData(OrganizationCreationAdmissionDecision.Unknown, "Organizations.CreationAdmissionUnavailable")]
    [InlineData((OrganizationCreationAdmissionDecision)999, "Organizations.CreationAdmissionUnavailable")]
    public async Task Product_decisions_have_stable_fail_closed_results(
        OrganizationCreationAdmissionDecision decision,
        string expectedErrorCode)
    {
        OrganizationCreationAdmissionPolicy policy = Create(
            enabled: true,
            new FixedPolicy(decision));

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(expectedErrorCode, result.Error.Code);
    }

    [Fact]
    public async Task Every_product_policy_must_allow_creation()
    {
        RecordingPolicy first = new(OrganizationCreationAdmissionDecision.Allowed);
        RecordingPolicy second = new(OrganizationCreationAdmissionDecision.Allowed);
        OrganizationCreationAdmissionPolicy policy = Create(enabled: true, first, second);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, first.InvocationCount);
        Assert.Equal(1, second.InvocationCount);
        Assert.Equal(Request, first.LastRequest);
    }

    [Fact]
    public async Task Product_policy_evaluation_stops_after_first_failure()
    {
        RecordingPolicy allowed = new(OrganizationCreationAdmissionDecision.Allowed);
        RecordingPolicy denied = new(OrganizationCreationAdmissionDecision.Denied);
        RecordingPolicy trailing = new(OrganizationCreationAdmissionDecision.Allowed);
        OrganizationCreationAdmissionPolicy policy = Create(
            enabled: true,
            allowed,
            denied,
            trailing);

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.CreationRejected, result.Error);
        Assert.Equal(1, allowed.InvocationCount);
        Assert.Equal(1, denied.InvocationCount);
        Assert.Equal(0, trailing.InvocationCount);
    }

    [Fact]
    public async Task Product_policy_exception_is_temporarily_unavailable()
    {
        OrganizationCreationAdmissionPolicy policy = Create(
            enabled: true,
            new ThrowingPolicy());

        Result result = await policy.AuthorizeAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.CreationAdmissionUnavailable, result.Error);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_to_unavailability()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OrganizationCreationAdmissionPolicy policy = Create(
            enabled: true,
            new ThrowingPolicy());

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await policy.AuthorizeAsync(Request, cancellation.Token));
    }

    private static OrganizationCreationAdmissionPolicy Create(
        bool enabled,
        params IOrganizationCreationAdmissionPolicy[] policies) =>
        new(
            Options.Create(new OrganizationsOptions
            {
                SelfServiceCreationEnabled = enabled
            }),
            policies);

    private sealed class FixedPolicy(OrganizationCreationAdmissionDecision decision)
        : IOrganizationCreationAdmissionPolicy
    {
        public ValueTask<OrganizationCreationAdmissionDecision> EvaluateAsync(
            OrganizationCreationAdmissionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(decision);
    }

    private sealed class RecordingPolicy(OrganizationCreationAdmissionDecision decision)
        : IOrganizationCreationAdmissionPolicy
    {
        public int InvocationCount { get; private set; }
        public OrganizationCreationAdmissionRequest? LastRequest { get; private set; }

        public ValueTask<OrganizationCreationAdmissionDecision> EvaluateAsync(
            OrganizationCreationAdmissionRequest request,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            this.LastRequest = request;
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class ThrowingPolicy : IOrganizationCreationAdmissionPolicy
    {
        public ValueTask<OrganizationCreationAdmissionDecision> EvaluateAsync(
            OrganizationCreationAdmissionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Policy unavailable.");
        }
    }
}
