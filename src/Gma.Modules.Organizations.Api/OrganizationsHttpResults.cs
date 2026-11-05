namespace Gma.Modules.Organizations.Api;

using Gma.Framework.Api.Results;
using Gma.Framework.Results;
using Microsoft.AspNetCore.Http;

/// <summary>Preserves the Organizations HTTP result contract in application API composition.</summary>
public static class OrganizationsHttpResults
{
    public static IResult ToOrganizationsHttpResult<TValue>(this Result<TValue> result) =>
        result.ToHttpResult(OrganizationEndpointSupport.ErrorStatusCodes);
}
