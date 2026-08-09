namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;

internal sealed partial class OrganizationMembershipChangePolicy(
    IEnumerable<IOrganizationMembershipChangePolicy> policies,
    ILogger<OrganizationMembershipChangePolicy>? logger = null)
{
    public async ValueTask<Result> AuthorizeAsync(
        OrganizationMembershipChangePolicyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (IOrganizationMembershipChangePolicy policy in policies)
        {
            OrganizationMembershipChangePolicyDecision decision;
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
                        request.RequestedStatus,
                        exception.GetType().Name);
                }

                return Result.Failure(
                    OrganizationApplicationErrors.MembershipChangeUnavailable);
            }

            if (decision == OrganizationMembershipChangePolicyDecision.Denied)
            {
                return Result.Failure(
                    OrganizationApplicationErrors.MembershipChangeRejected);
            }

            if (decision != OrganizationMembershipChangePolicyDecision.Allowed)
            {
                return Result.Failure(
                    OrganizationApplicationErrors.MembershipChangeUnavailable);
            }
        }

        return Result.Success();
    }

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Warning,
        Message = "Organization membership-change policy {PolicyType} failed for requested status {RequestedStatus} with {ExceptionType}.")]
    private static partial void LogPolicyFailure(
        ILogger logger,
        string? policyType,
        OrganizationMembershipStatus requestedStatus,
        string exceptionType);
}
