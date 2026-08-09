namespace Gma.Modules.Organizations.Tests;

using System.Security.Claims;
using Gma.Framework.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Security;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationEndpointSupportTests
{
    [Fact]
    public async Task Configured_assurance_protects_governance_mutations_only()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.Configure<OrganizationsApiSecurityOptions>(options =>
            options.GovernanceOperationsAssurance = new AuthenticationAssuranceRequirement(
                maxAuthenticationAge: TimeSpan.FromMinutes(10)));
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        await using WebApplication app = builder.Build();

        new OrganizationsModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];
        AssertAssurance(endpoints, HttpMethods.Post, "/api/organizations/", expected: true);
        AssertAssurance(endpoints, HttpMethods.Put, "/api/organizations/{organizationId:guid}", expected: true);
        AssertAssurance(endpoints, HttpMethods.Post, "/api/organizations/{organizationId:guid}/members/suspend", expected: true);
        AssertAssurance(endpoints, HttpMethods.Post, "/api/organizations/{organizationId:guid}/ownership/transfer", expected: true);
        AssertAssurance(endpoints, HttpMethods.Post, "/api/organizations/{organizationId:guid}/invitations", expected: true);
        AssertAssurance(endpoints, HttpMethods.Post, "/api/organizations/{organizationId:guid}/enrollment-links", expected: true);
        AssertAssurance(endpoints, HttpMethods.Get, "/api/organizations/{organizationId:guid}/members", expected: false);
        AssertAssurance(endpoints, HttpMethods.Post, "/api/organization-invitations/accept", expected: false);
        AssertAssurance(endpoints, HttpMethods.Post, "/api/organization-enrollment/claim", expected: false);
    }

    [Fact]
    public void Subject_verification_failure_is_forbidden()
    {
        int statusCode = OrganizationEndpointSupport.ErrorStatusCodes.GetStatusCode(
            OrganizationApplicationErrors.SubjectVerificationRequired);

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Theory]
    [InlineData("Organizations.CreationOperationRequired", StatusCodes.Status400BadRequest)]
    [InlineData("Organizations.CreationOperationConflict", StatusCodes.Status409Conflict)]
    public void Creation_operation_failures_have_stable_http_statuses(
        string errorCode,
        int expectedStatusCode)
    {
        Error error = errorCode ==
            OrganizationApplicationErrors.CreationOperationRequired.Code
                ? OrganizationApplicationErrors.CreationOperationRequired
                : OrganizationApplicationErrors.CreationOperationConflict;

        Assert.Equal(
            expectedStatusCode,
            OrganizationEndpointSupport.ErrorStatusCodes.GetStatusCode(error));
    }

    [Theory]
    [InlineData("Organizations.JoinSourceIdRequired", StatusCodes.Status400BadRequest)]
    [InlineData("Organizations.JoinSourceIssuanceConflict", StatusCodes.Status409Conflict)]
    public void Join_source_issuance_failures_have_stable_http_statuses(
        string errorCode,
        int expectedStatusCode)
    {
        Error error = errorCode == OrganizationApplicationErrors.JoinSourceIdRequired.Code
            ? OrganizationApplicationErrors.JoinSourceIdRequired
            : OrganizationApplicationErrors.JoinSourceIssuanceConflict;

        Assert.Equal(
            expectedStatusCode,
            OrganizationEndpointSupport.ErrorStatusCodes.GetStatusCode(error));
    }

    [Theory]
    [InlineData("Organizations.MutationRejected", StatusCodes.Status409Conflict)]
    [InlineData("Organizations.MutationAdmissionUnavailable", StatusCodes.Status503ServiceUnavailable)]
    public void Mutation_admission_failures_have_stable_http_statuses(
        string errorCode,
        int expectedStatusCode)
    {
        Error error = errorCode == OrganizationApplicationErrors.MutationRejected.Code
            ? OrganizationApplicationErrors.MutationRejected
            : OrganizationApplicationErrors.MutationAdmissionUnavailable;

        int statusCode = OrganizationEndpointSupport.ErrorStatusCodes.GetStatusCode(error);

        Assert.Equal(expectedStatusCode, statusCode);
    }

    [Theory]
    [InlineData(ApplicationClaimNames.Subject)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public void Subject_can_be_resolved_from_raw_or_mapped_jwt_claim(string claimType)
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(claimType, " member-1 ")],
                authenticationType: "test"))
        };

        bool resolved = OrganizationEndpointSupport.TryGetSubject(context, out string subjectId);

        Assert.True(resolved);
        Assert.Equal("member-1", subjectId);
    }

    [Fact]
    public void Sensitive_organization_responses_disable_caching()
    {
        DefaultHttpContext context = new();

        OrganizationEndpointSupport.SetNoStoreHeaders(context);

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
    }

    private static void AssertAssurance(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route,
        bool expected)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.Ordinal) == true);
        bool configured = endpoint.Metadata.Any(metadata =>
            string.Equals(metadata.GetType().Name, "AuthenticationAssuranceMetadata", StringComparison.Ordinal));
        Assert.Equal(expected, configured);
    }
}
