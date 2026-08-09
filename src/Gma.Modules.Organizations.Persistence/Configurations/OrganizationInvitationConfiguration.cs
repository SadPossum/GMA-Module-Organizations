namespace Gma.Modules.Organizations.Persistence.Configurations;

using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable(
            "organization_invitations",
            table => table.HasCheckConstraint(
                "CK_organization_invitations_replacement_lineage",
                "(\"ReplacesInvitationId\" IS NULL AND " +
                "\"ReplacesInvitationVersion\" IS NULL) OR " +
                "(\"ReplacesInvitationId\" IS NOT NULL AND " +
                "\"ReplacesInvitationId\" <> \"Id\" AND " +
                "\"ReplacesInvitationVersion\" > 0)"));
        builder.HasKey(invitation => invitation.Id);
        builder.Ignore(invitation => invitation.DomainEvents);
        builder.Property(invitation => invitation.InviterSubjectId)
            .HasMaxLength(OrganizationSubjectId.MaxLength)
            .IsRequired();
        builder.Property(invitation => invitation.RecipientEmail)
            .HasMaxLength(OrganizationInvitationRecipient.MaxLength);
        builder.Property(invitation => invitation.TokenDigest)
            .HasMaxLength(OrganizationInvitation.TokenDigestLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(invitation => invitation.Status).HasConversion<int>();
        builder.Property(invitation => invitation.AcceptedSubjectId)
            .HasMaxLength(OrganizationSubjectId.MaxLength);
        builder.Property(invitation => invitation.Version).IsConcurrencyToken();
        builder.Property(invitation => invitation.CreatedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.Property(invitation => invitation.LastChangedBy)
            .HasMaxLength(OrganizationActorId.MaxLength)
            .IsRequired();
        builder.HasIndex(invitation => invitation.TokenDigest).IsUnique();
        builder.HasIndex(invitation => invitation.ReplacesInvitationId).IsUnique();
        builder.HasIndex(invitation => new
        {
            invitation.OrganizationId,
            invitation.Status,
            invitation.CreatedAtUtc
        });
        builder.HasIndex(invitation => new { invitation.Status, invitation.ExpiresAtUtc });
        builder.HasIndex(invitation => new { invitation.Status, invitation.LastChangedAtUtc });
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(invitation => invitation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
