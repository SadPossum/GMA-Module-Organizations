using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddJoinSourceReplacementLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesInvitationId",
                schema: "organizations",
                table: "organization_invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReplacesInvitationVersion",
                schema: "organizations",
                table: "organization_invitations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesEnrollmentLinkId",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReplacesEnrollmentLinkVersion",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_ReplacesInvitationId",
                schema: "organizations",
                table: "organization_invitations",
                column: "ReplacesInvitationId",
                unique: true,
                filter: "[ReplacesInvitationId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_invitations_replacement_lineage",
                schema: "organizations",
                table: "organization_invitations",
                sql: "(\"ReplacesInvitationId\" IS NULL AND \"ReplacesInvitationVersion\" IS NULL) OR (\"ReplacesInvitationId\" IS NOT NULL AND \"ReplacesInvitationId\" <> \"Id\" AND \"ReplacesInvitationVersion\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_links_ReplacesEnrollmentLinkId",
                schema: "organizations",
                table: "organization_enrollment_links",
                column: "ReplacesEnrollmentLinkId",
                unique: true,
                filter: "[ReplacesEnrollmentLinkId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_enrollment_links_replacement_lineage",
                schema: "organizations",
                table: "organization_enrollment_links",
                sql: "(\"ReplacesEnrollmentLinkId\" IS NULL AND \"ReplacesEnrollmentLinkVersion\" IS NULL) OR (\"ReplacesEnrollmentLinkId\" IS NOT NULL AND \"ReplacesEnrollmentLinkId\" <> \"Id\" AND \"ReplacesEnrollmentLinkVersion\" > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organization_invitations_ReplacesInvitationId",
                schema: "organizations",
                table: "organization_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_invitations_replacement_lineage",
                schema: "organizations",
                table: "organization_invitations");

            migrationBuilder.DropIndex(
                name: "IX_organization_enrollment_links_ReplacesEnrollmentLinkId",
                schema: "organizations",
                table: "organization_enrollment_links");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_enrollment_links_replacement_lineage",
                schema: "organizations",
                table: "organization_enrollment_links");

            migrationBuilder.DropColumn(
                name: "ReplacesInvitationId",
                schema: "organizations",
                table: "organization_invitations");

            migrationBuilder.DropColumn(
                name: "ReplacesInvitationVersion",
                schema: "organizations",
                table: "organization_invitations");

            migrationBuilder.DropColumn(
                name: "ReplacesEnrollmentLinkId",
                schema: "organizations",
                table: "organization_enrollment_links");

            migrationBuilder.DropColumn(
                name: "ReplacesEnrollmentLinkVersion",
                schema: "organizations",
                table: "organization_enrollment_links");
        }
    }
}
