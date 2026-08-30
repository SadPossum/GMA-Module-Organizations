namespace Gma.Modules.Organizations.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;

internal sealed class OrganizationProvisioner(IRequestDispatcher dispatcher)
    : IOrganizationProvisioner
{
    public async Task<OrganizationProvisioningResult> ProvisionAsync(
        OrganizationProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<OrganizationProvisioningResult> result = await dispatcher.SendAsync(
            new ProvisionOrganizationCommand(
                request.OrganizationId,
                request.Name,
                request.Slug,
                request.InitialOwnerSubjectId,
                request.ActorId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }
}
