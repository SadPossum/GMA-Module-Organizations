namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed partial class OrganizationCreationAdmissionPolicy(
    IOptions<OrganizationsOptions> options,
    IEnumerable<IOrganizationCreationAdmissionPolicy> policies,
    ILogger<OrganizationCreationAdmissionPolicy>? logger = null)
{
    public async ValueTask<Result> AuthorizeAsync(
        OrganizationCreationAdmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.Value.SelfServiceCreationEnabled)
        {
            return Result.Failure(
                OrganizationApplicationErrors.SelfServiceCreationDisabled);
        }

        foreach (IOrganizationCreationAdmissionPolicy policy in policies)
        {
            OrganizationCreationAdmissionDecision decision;
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

                return Result.Failure(
                    OrganizationApplicationErrors.CreationAdmissionUnavailable);
            }

            if (decision == OrganizationCreationAdmissionDecision.Denied)
            {
                return Result.Failure(
                    OrganizationApplicationErrors.CreationRejected);
            }

            if (decision == OrganizationCreationAdmissionDecision.SubjectVerificationRequired)
            {
                return Result.Failure(
                    OrganizationApplicationErrors.SubjectVerificationRequired);
            }

            if (decision != OrganizationCreationAdmissionDecision.Allowed)
            {
                return Result.Failure(
                    OrganizationApplicationErrors.CreationAdmissionUnavailable);
            }
        }

        return Result.Success();
    }

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Warning,
        Message = "Organization creation admission policy {PolicyType} failed with {ExceptionType}.")]
    private static partial void LogPolicyFailure(
        ILogger logger,
        string? policyType,
        string exceptionType);
}
