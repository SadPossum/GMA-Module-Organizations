namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;

internal sealed partial class OrganizationMutationAdmissionPolicy(
    IEnumerable<IOrganizationMutationAdmissionPolicy> policies,
    ILogger<OrganizationMutationAdmissionPolicy>? logger = null)
{
    public async ValueTask<Result> AuthorizeAsync(
        OrganizationMutationAdmissionContext context,
        CancellationToken cancellationToken)
    {
        foreach (IOrganizationMutationAdmissionPolicy policy in policies)
        {
            OrganizationMutationAdmissionDecision decision;
            try
            {
                decision = await policy.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
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

                return Result.Failure(OrganizationApplicationErrors.MutationAdmissionUnavailable);
            }

            if (decision is OrganizationMutationAdmissionDecision.Denied)
            {
                return Result.Failure(OrganizationApplicationErrors.MutationRejected);
            }

            if (decision is not OrganizationMutationAdmissionDecision.Allowed)
            {
                return Result.Failure(OrganizationApplicationErrors.MutationAdmissionUnavailable);
            }
        }

        return Result.Success();
    }

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "Organization mutation admission policy {PolicyType} failed for operation {Operation} with {ExceptionType}.")]
    private static partial void LogPolicyFailure(
        ILogger logger,
        string? policyType,
        OrganizationMutationAdmissionOperation operation,
        string exceptionType);
}
