namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;

internal sealed partial class OrganizationInvitationRecipientVerification(
    IEnumerable<IOrganizationInvitationRecipientVerificationPolicy> policies,
    ILogger<OrganizationInvitationRecipientVerification>? logger = null)
{
    public async ValueTask<Result> VerifyAsync(
        OrganizationInvitationRecipientVerificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        bool unavailable = false;
        foreach (IOrganizationInvitationRecipientVerificationPolicy policy in policies)
        {
            OrganizationInvitationRecipientVerificationDecision decision;
            try
            {
                decision = await policy.EvaluateAsync(request, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (logger is not null)
                {
                    LogPolicyFailure(
                        logger,
                        policy.GetType().FullName,
                        exception.GetType().Name);
                }

                unavailable = true;
                continue;
            }

            if (decision == OrganizationInvitationRecipientVerificationDecision.Verified)
            {
                return Result.Success();
            }

            if (decision != OrganizationInvitationRecipientVerificationDecision.NotVerified)
            {
                unavailable = true;
            }
        }

        return Result.Failure(
            unavailable
                ? OrganizationApplicationErrors.RecipientVerificationUnavailable
                : OrganizationApplicationErrors.RecipientVerificationRequired);
    }

    [LoggerMessage(
        EventId = 4105,
        Level = LogLevel.Warning,
        Message = "Organization invitation recipient verifier {PolicyType} failed with {ExceptionType}.")]
    private static partial void LogPolicyFailure(
        ILogger logger,
        string? policyType,
        string exceptionType);
}
