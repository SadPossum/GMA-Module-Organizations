namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Framework.Results;

public interface IOrganizationAdmissionPolicy
{
    Task<Result> CanCreateOrganizationAsync(string subjectId, CancellationToken cancellationToken);
}
