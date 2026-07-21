namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationMembershipChangePolicyRequest
{
    public OrganizationMembershipChangePolicyRequest(
        Guid organizationId,
        string actingSubjectId,
        string targetSubjectId,
        OrganizationMembershipRole currentRole,
        OrganizationMembershipStatus currentStatus,
        OrganizationMembershipStatus requestedStatus)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        this.OrganizationId = organizationId;
        this.ActingSubjectId = RequireSubject(actingSubjectId, nameof(actingSubjectId));
        this.TargetSubjectId = RequireSubject(targetSubjectId, nameof(targetSubjectId));
        this.CurrentRole = RequireDefined(currentRole, nameof(currentRole));
        this.CurrentStatus = RequireDefined(currentStatus, nameof(currentStatus));
        this.RequestedStatus = RequireDefined(requestedStatus, nameof(requestedStatus));
    }

    public Guid OrganizationId { get; }
    public string ActingSubjectId { get; }
    public string TargetSubjectId { get; }
    public OrganizationMembershipRole CurrentRole { get; }
    public OrganizationMembershipStatus CurrentStatus { get; }
    public OrganizationMembershipStatus RequestedStatus { get; }

    private static string RequireSubject(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 200
            ? normalized
            : throw new ArgumentException("Subject id is invalid.", parameterName);
    }

    private static TEnum RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) && Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName);
}
