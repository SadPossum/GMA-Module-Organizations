namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Microsoft.Extensions.Options;

internal static class OrganizationInvitationHandlerSupport
{
    public static Result<int> ResolveLifetimeHours(int? requested, IOptions<OrganizationsOptions> options)
    {
        int lifetime = requested ?? options.Value.InvitationDefaultLifetimeHours;
        return lifetime is >= 1 && lifetime <= options.Value.InvitationMaxLifetimeHours
            ? Result.Success(lifetime)
            : Result.Failure<int>(OrganizationApplicationErrors.InvitationLifetimeInvalid);
    }
}
