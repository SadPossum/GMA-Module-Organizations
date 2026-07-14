namespace Gma.Modules.Organizations.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record PreviewOrganizationEnrollmentLinkQuery(string Token)
    : IQuery<OrganizationEnrollmentPreviewDto>;
