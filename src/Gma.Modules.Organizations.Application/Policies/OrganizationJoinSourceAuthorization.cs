namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.Extensions.Logging;
using DomainMembershipRole =
    Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;

internal sealed partial class OrganizationJoinSourceAuthorization(
    IOrganizationRepository organizations,
    IEnumerable<IOrganizationJoinSourceAuthorizationPolicy> policies,
    ILogger<OrganizationJoinSourceAuthorization>? logger = null)
{
    public async Task<Result> AuthorizeAsync(
        OrganizationJoinSourceAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Operation == OrganizationJoinSourceAuthorizationOperation.Unknown ||
            !Enum.IsDefined(context.Operation) ||
            context.OrganizationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(context.SubjectId))
        {
            return Result.Failure(
                OrganizationApplicationErrors.JoinSourceAuthorizationUnavailable);
        }

        Result<OrganizationMembership> membership =
            await OrganizationMembershipAuthorization.RequireActiveAsync(
                organizations,
                context.OrganizationId,
                context.SubjectId,
                cancellationToken).ConfigureAwait(false);
        if (membership.IsFailure)
        {
            return Result.Failure(membership.Error);
        }

        if (membership.Value.Role == DomainMembershipRole.Owner)
        {
            return Result.Success();
        }

        bool allowed = false;
        foreach (IOrganizationJoinSourceAuthorizationPolicy policy in policies)
        {
            OrganizationJoinSourceAuthorizationDecision decision;
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
                    OrganizationApplicationErrors.JoinSourceAuthorizationUnavailable);
            }

            switch (decision)
            {
                case OrganizationJoinSourceAuthorizationDecision.NotApplicable:
                    continue;
                case OrganizationJoinSourceAuthorizationDecision.Allowed:
                    allowed = true;
                    continue;
                case OrganizationJoinSourceAuthorizationDecision.Denied:
                    return Result.Failure(
                        OrganizationApplicationErrors.JoinSourceManagementRequired);
                default:
                    return Result.Failure(
                        OrganizationApplicationErrors.JoinSourceAuthorizationUnavailable);
            }
        }

        return allowed
            ? Result.Success()
            : Result.Failure(
                OrganizationApplicationErrors.JoinSourceManagementRequired);
    }

    [LoggerMessage(
        EventId = 4111,
        Level = LogLevel.Warning,
        Message = "Organization join-source authorization policy {PolicyType} failed for operation {Operation} with {ExceptionType}.")]
    private static partial void LogPolicyFailure(
        ILogger logger,
        string? policyType,
        OrganizationJoinSourceAuthorizationOperation operation,
        string exceptionType);
}
