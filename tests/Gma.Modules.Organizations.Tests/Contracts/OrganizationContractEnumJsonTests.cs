namespace Gma.Modules.Organizations.Tests;

using System.Text.Json;
using Gma.Modules.Organizations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationContractEnumJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static TheoryData<Type, object, string> ContractEnumCases =>
        new()
        {
            { typeof(OrganizationJoinAdmissionOperation), OrganizationJoinAdmissionOperation.ApproveEnrollment, "approve-enrollment" },
            { typeof(OrganizationMutationAdmissionDecision), OrganizationMutationAdmissionDecision.Unavailable, "unavailable" },
            { typeof(OrganizationMutationAdmissionOperation), OrganizationMutationAdmissionOperation.RotateEnrollmentLink, "rotate-enrollment-link" },
            { typeof(OrganizationMutationAdmissionOperation), OrganizationMutationAdmissionOperation.RestoreMembership, "restore-membership" },
            { typeof(OrganizationJoinSourceIssuanceOutcome), OrganizationJoinSourceIssuanceOutcome.AlreadyIssued, "already-issued" },
            { typeof(OrganizationMembershipChangePolicyDecision), OrganizationMembershipChangePolicyDecision.Allowed, "allowed" },
            { typeof(OrganizationMembershipLifecycleOutcome), OrganizationMembershipLifecycleOutcome.AlreadyInDesiredState, "already-in-desired-state" },
            { typeof(OrganizationChange), OrganizationChange.OwnershipTransferred, "ownership-transferred" },
            { typeof(OrganizationEnrollmentApprovalMode), OrganizationEnrollmentApprovalMode.RequiresApproval, "requires-approval" },
            { typeof(OrganizationEnrollmentClaimChange), OrganizationEnrollmentClaimChange.Requested, "requested" },
            { typeof(OrganizationEnrollmentClaimStatus), OrganizationEnrollmentClaimStatus.Expired, "expired" },
            { typeof(OrganizationEnrollmentLinkChange), OrganizationEnrollmentLinkChange.ClaimReserved, "claim-reserved" },
            { typeof(OrganizationEnrollmentLinkStatus), OrganizationEnrollmentLinkStatus.CapacityReached, "capacity-reached" },
            { typeof(OrganizationInvitationChange), OrganizationInvitationChange.Superseded, "superseded" },
            { typeof(OrganizationInvitationStatus), OrganizationInvitationStatus.Expired, "expired" },
            { typeof(OrganizationMembershipChange), OrganizationMembershipChange.PromotedToOwner, "promoted-to-owner" },
            { typeof(OrganizationMembershipRole), OrganizationMembershipRole.Owner, "owner" },
            { typeof(OrganizationMembershipStatus), OrganizationMembershipStatus.Removed, "removed" },
            { typeof(OrganizationStatus), OrganizationStatus.Archived, "archived" }
        };

    [Theory]
    [MemberData(nameof(ContractEnumCases))]
    public void Contract_enums_use_stable_wire_names(Type enumType, object value, string wireName)
    {
        string json = JsonSerializer.Serialize(value, enumType, JsonOptions);

        Assert.Equal($"\"{wireName}\"", json);
        Assert.Equal(value, JsonSerializer.Deserialize(json, enumType, JsonOptions));
    }

    [Theory]
    [MemberData(nameof(ContractEnumCases))]
    public void Contract_enums_reject_numeric_unknown_and_future_values(
        Type enumType,
        object value,
        string wireName)
    {
        _ = value;
        _ = wireName;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("1", enumType, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("\"unknown\"", enumType, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("\"future\"", enumType, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(Enum.ToObject(enumType, 0), enumType, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(Enum.ToObject(enumType, 999), enumType, JsonOptions));
    }
}
