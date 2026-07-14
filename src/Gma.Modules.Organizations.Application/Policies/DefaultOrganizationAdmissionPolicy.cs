namespace Gma.Modules.Organizations.Application.Policies;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Ports;
using Microsoft.Extensions.Options;

internal sealed class DefaultOrganizationAdmissionPolicy(IOptions<OrganizationsOptions> options)
    : IOrganizationAdmissionPolicy
{
    public Task<Result> CanCreateOrganizationAsync(string subjectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(options.Value.SelfServiceCreationEnabled
            ? Result.Success()
            : Result.Failure(OrganizationApplicationErrors.SelfServiceCreationDisabled));
    }
}
