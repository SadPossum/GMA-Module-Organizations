namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;

internal sealed partial class OrganizationJoinAdmissionPolicy(
    IEnumerable<IOrganizationJoinAdmissionPolicy> policies,
    ILogger<OrganizationJoinAdmissionPolicy>? logger = null)
{
    public async ValueTask<Result> AuthorizeAsync(
        OrganizationJoinAdmissionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (IOrganizationJoinAdmissionPolicy policy in policies)
        {
            OrganizationJoinAdmissionDecision decision;
            try
            {
                decision = await policy.EvaluateAsync(context, cancellationToken)
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
                        context.Operation,
                        exception.GetType().Name);
                }

                return Result.Failure(
                    OrganizationApplicationErrors.JoinAdmissionUnavailable);
            }

            if (decision == OrganizationJoinAdmissionDecision.Denied)
            {
                return Result.Failure(
                    OrganizationApplicationErrors.JoinAdmissionRejected);
            }

            if (decision != OrganizationJoinAdmissionDecision.Allowed)
            {
                return Result.Failure(
                    OrganizationApplicationErrors.JoinAdmissionUnavailable);
            }
        }

        return Result.Success();
    }

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Warning,
        Message = "Organization join admission policy {PolicyType} failed for operation {Operation} with {ExceptionType}.")]
    private static partial void LogPolicyFailure(
        ILogger logger,
        string? policyType,
        OrganizationJoinAdmissionOperation operation,
        string exceptionType);
}
