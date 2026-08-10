namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationInvitationRecipientVerificationTests
{
    private static readonly OrganizationInvitationRecipientVerificationRequest Request = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "subject-a",
        "person@example.test");

    [Fact]
    public async Task Recipient_bound_invitation_fails_closed_without_verifiers()
    {
        OrganizationInvitationRecipientVerification verification = new([]);

        Result result = await verification.VerifyAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.RecipientVerificationRequired, result.Error);
    }

    [Fact]
    public async Task Any_trusted_verifier_can_prove_the_recipient()
    {
        RecordingPolicy notVerified = new(
            OrganizationInvitationRecipientVerificationDecision.NotVerified);
        RecordingPolicy verified = new(
            OrganizationInvitationRecipientVerificationDecision.Verified);
        RecordingPolicy trailing = new(
            OrganizationInvitationRecipientVerificationDecision.NotVerified);
        OrganizationInvitationRecipientVerification verification = new(
            [notVerified, verified, trailing]);

        Result result = await verification.VerifyAsync(Request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, notVerified.InvocationCount);
        Assert.Equal(1, verified.InvocationCount);
        Assert.Equal(0, trailing.InvocationCount);
        Assert.Equal(Request, verified.LastRequest);
    }

    [Fact]
    public async Task Unanimous_lack_of_proof_requires_recipient_verification()
    {
        OrganizationInvitationRecipientVerification verification = new(
            [
                new FixedPolicy(OrganizationInvitationRecipientVerificationDecision.NotVerified),
                new FixedPolicy(OrganizationInvitationRecipientVerificationDecision.NotVerified)
            ]);

        Result result = await verification.VerifyAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.RecipientVerificationRequired, result.Error);
    }

    [Theory]
    [InlineData(OrganizationInvitationRecipientVerificationDecision.Unavailable)]
    [InlineData(OrganizationInvitationRecipientVerificationDecision.Unknown)]
    [InlineData((OrganizationInvitationRecipientVerificationDecision)999)]
    public async Task Indeterminate_decision_wins_over_lack_of_proof(
        OrganizationInvitationRecipientVerificationDecision decision)
    {
        OrganizationInvitationRecipientVerification verification = new(
            [
                new FixedPolicy(OrganizationInvitationRecipientVerificationDecision.NotVerified),
                new FixedPolicy(decision)
            ]);

        Result result = await verification.VerifyAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.RecipientVerificationUnavailable, result.Error);
    }

    [Fact]
    public async Task Later_verifier_can_prove_recipient_after_provider_exception()
    {
        OrganizationInvitationRecipientVerification verification = new(
            [
                new ThrowingPolicy(),
                new FixedPolicy(OrganizationInvitationRecipientVerificationDecision.Verified)
            ]);

        Result result = await verification.VerifyAsync(Request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Provider_exception_without_proof_is_temporarily_unavailable()
    {
        OrganizationInvitationRecipientVerification verification = new(
            [new ThrowingPolicy()]);

        Result result = await verification.VerifyAsync(Request, CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.RecipientVerificationUnavailable, result.Error);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_to_unavailability()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OrganizationInvitationRecipientVerification verification = new(
            [new ThrowingPolicy()]);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await verification.VerifyAsync(Request, cancellation.Token));
    }

    private sealed class FixedPolicy(
        OrganizationInvitationRecipientVerificationDecision decision)
        : IOrganizationInvitationRecipientVerificationPolicy
    {
        public ValueTask<OrganizationInvitationRecipientVerificationDecision> EvaluateAsync(
            OrganizationInvitationRecipientVerificationRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(decision);
    }

    private sealed class RecordingPolicy(
        OrganizationInvitationRecipientVerificationDecision decision)
        : IOrganizationInvitationRecipientVerificationPolicy
    {
        public int InvocationCount { get; private set; }
        public OrganizationInvitationRecipientVerificationRequest? LastRequest { get; private set; }

        public ValueTask<OrganizationInvitationRecipientVerificationDecision> EvaluateAsync(
            OrganizationInvitationRecipientVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            this.LastRequest = request;
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class ThrowingPolicy
        : IOrganizationInvitationRecipientVerificationPolicy
    {
        public ValueTask<OrganizationInvitationRecipientVerificationDecision> EvaluateAsync(
            OrganizationInvitationRecipientVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Verifier unavailable.");
        }
    }
}
