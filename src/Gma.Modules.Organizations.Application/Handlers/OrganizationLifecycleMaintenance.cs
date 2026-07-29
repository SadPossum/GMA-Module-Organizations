namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

internal static class OrganizationLifecycleMaintenance
{
    public const string ActorId = "system:organizations-lifecycle";

    public static Result ValidateBatchSize(int batchSize) =>
        batchSize is >= 1 and <= 10_000
            ? Result.Success()
            : Result.Failure(OrganizationDomainErrors.EnrollmentConfigurationInvalid);
}
