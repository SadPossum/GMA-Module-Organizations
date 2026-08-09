namespace Gma.Modules.Organizations.Tests.Domain;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Domain.Enums;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationJoinSourceReplacementLineageTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Invitation_replacement_lineage_is_complete_non_recursive_and_exported()
    {
        Guid invitationId = Guid.NewGuid();
        Guid predecessorId = Guid.NewGuid();

        Result<OrganizationInvitation> valid = CreateInvitation(
            invitationId,
            predecessorId,
            4);

        Assert.True(valid.IsSuccess, valid.Error.Code);
        Assert.Equal(predecessorId, valid.Value.ReplacesInvitationId);
        Assert.Equal(4, valid.Value.ReplacesInvitationVersion);
        Assert.Equal(predecessorId, valid.Value.ToDto(Now).ReplacesInvitationId);
        AssertInvalidInvitation(invitationId, predecessorId, null);
        AssertInvalidInvitation(invitationId, null, 4);
        AssertInvalidInvitation(invitationId, Guid.Empty, 4);
        AssertInvalidInvitation(invitationId, invitationId, 4);
        AssertInvalidInvitation(invitationId, predecessorId, 0);
    }

    [Fact]
    public void Enrollment_replacement_lineage_is_complete_non_recursive_and_exported()
    {
        Guid enrollmentLinkId = Guid.NewGuid();
        Guid predecessorId = Guid.NewGuid();

        Result<OrganizationEnrollmentLink> valid = CreateEnrollmentLink(
            enrollmentLinkId,
            predecessorId,
            7);

        Assert.True(valid.IsSuccess, valid.Error.Code);
        Assert.Equal(predecessorId, valid.Value.ReplacesEnrollmentLinkId);
        Assert.Equal(7, valid.Value.ReplacesEnrollmentLinkVersion);
        Assert.Equal(predecessorId, valid.Value.ToDto(Now).ReplacesEnrollmentLinkId);
        AssertInvalidEnrollmentLink(enrollmentLinkId, predecessorId, null);
        AssertInvalidEnrollmentLink(enrollmentLinkId, null, 7);
        AssertInvalidEnrollmentLink(enrollmentLinkId, Guid.Empty, 7);
        AssertInvalidEnrollmentLink(enrollmentLinkId, enrollmentLinkId, 7);
        AssertInvalidEnrollmentLink(enrollmentLinkId, predecessorId, 0);
    }

    private static void AssertInvalidInvitation(
        Guid invitationId,
        Guid? predecessorId,
        long? predecessorVersion)
    {
        Result<OrganizationInvitation> result = CreateInvitation(
            invitationId,
            predecessorId,
            predecessorVersion);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationDomainErrors.InvitationReplacementInvalid, result.Error);
    }

    private static void AssertInvalidEnrollmentLink(
        Guid enrollmentLinkId,
        Guid? predecessorId,
        long? predecessorVersion)
    {
        Result<OrganizationEnrollmentLink> result = CreateEnrollmentLink(
            enrollmentLinkId,
            predecessorId,
            predecessorVersion);

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationDomainErrors.EnrollmentLinkReplacementInvalid, result.Error);
    }

    private static Result<OrganizationInvitation> CreateInvitation(
        Guid invitationId,
        Guid? predecessorId,
        long? predecessorVersion) =>
        OrganizationInvitation.Create(
            invitationId,
            Guid.NewGuid(),
            "owner",
            null,
            new string('a', 64),
            Now.AddHours(24),
            "user:owner",
            Guid.NewGuid(),
            Now,
            predecessorId,
            predecessorVersion);

    private static Result<OrganizationEnrollmentLink> CreateEnrollmentLink(
        Guid enrollmentLinkId,
        Guid? predecessorId,
        long? predecessorVersion) =>
        OrganizationEnrollmentLink.Create(
            enrollmentLinkId,
            Guid.NewGuid(),
            "owner",
            new string('a', 64),
            Now.AddHours(24),
            10,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner",
            Guid.NewGuid(),
            Now,
            predecessorId,
            predecessorVersion);
}
