namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Handlers;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Queries;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using DomainEnrollmentMode = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationJoinSourceLookupHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Owner_can_read_exact_join_sources()
    {
        TestOrganizationRepository repository = CreateRepository();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationInvitation invitation = OrganizationInvitation.Create(
            Guid.NewGuid(), organization.Id, "owner", "staff@example.test", new string('a', 64),
            Now.AddDays(1), "owner", Guid.NewGuid(), Now).Value;
        OrganizationEnrollmentLink link = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organization.Id, "owner", new string('b', 64), Now.AddDays(1), 10,
            DomainEnrollmentMode.Automatic, "owner", Guid.NewGuid(), Now).Value;
        repository.Invitations.Add(invitation);
        repository.EnrollmentLinks.Add(link);

        OrganizationJoinSourceAuthorization authorization = Authorization(repository);
        GetOrganizationInvitationQueryHandler invitations = new(
            repository,
            authorization,
            new TestClock(Now));
        GetOrganizationEnrollmentLinkQueryHandler links = new(
            repository,
            authorization,
            new TestClock(Now));

        Result<OrganizationInvitationDto> selectedInvitation = await invitations.HandleAsync(
            new GetOrganizationInvitationQuery(organization.Id, invitation.Id, "owner"),
            CancellationToken.None);
        Result<OrganizationEnrollmentLinkDto> selectedLink = await links.HandleAsync(
            new GetOrganizationEnrollmentLinkQuery(organization.Id, link.Id, "owner"),
            CancellationToken.None);

        Assert.Equal(invitation.Id, selectedInvitation.Value.InvitationId);
        Assert.Equal(link.Id, selectedLink.Value.EnrollmentLinkId);
        Assert.Equal(2, repository.MembershipReadCount);
        Assert.Equal(1, repository.InvitationReadCount);
        Assert.Equal(1, repository.EnrollmentLinkReadCount);
    }

    [Fact]
    public async Task Non_owner_is_denied_before_join_source_state_is_read()
    {
        TestOrganizationRepository repository = CreateRepository();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationJoinSourceAuthorization authorization = Authorization(repository);
        GetOrganizationInvitationQueryHandler invitations = new(
            repository,
            authorization,
            new TestClock(Now));
        GetOrganizationEnrollmentLinkQueryHandler links = new(
            repository,
            authorization,
            new TestClock(Now));

        Result<OrganizationInvitationDto> selectedInvitation = await invitations.HandleAsync(
            new GetOrganizationInvitationQuery(organization.Id, Guid.NewGuid(), "not-an-owner"),
            CancellationToken.None);
        Result<OrganizationEnrollmentLinkDto> selectedLink = await links.HandleAsync(
            new GetOrganizationEnrollmentLinkQuery(organization.Id, Guid.NewGuid(), "not-an-owner"),
            CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.MembershipRequired, selectedInvitation.Error);
        Assert.Equal(OrganizationApplicationErrors.MembershipRequired, selectedLink.Error);
        Assert.Equal(2, repository.MembershipReadCount);
        Assert.Equal(0, repository.InvitationReadCount);
        Assert.Equal(0, repository.EnrollmentLinkReadCount);
    }

    private static TestOrganizationRepository CreateRepository()
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(), "Harbor House", "harbor-house", "owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "owner", DomainMembershipRole.Owner,
            "owner", Guid.NewGuid(), Now).Value;
        return new TestOrganizationRepository(organization, owner);
    }

    private static OrganizationJoinSourceAuthorization Authorization(
        TestOrganizationRepository repository) => new(
        repository,
        [],
        NullLogger<OrganizationJoinSourceAuthorization>.Instance);
}
