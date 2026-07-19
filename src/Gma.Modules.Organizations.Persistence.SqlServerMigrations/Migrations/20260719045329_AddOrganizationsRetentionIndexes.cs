using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsRetentionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_Status_LastChangedAtUtc",
                schema: "organizations",
                table: "organization_invitations",
                columns: new[] { "Status", "LastChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_links_Status_LastChangedAtUtc",
                schema: "organizations",
                table: "organization_enrollment_links",
                columns: new[] { "Status", "LastChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organization_invitations_Status_LastChangedAtUtc",
                schema: "organizations",
                table: "organization_invitations");

            migrationBuilder.DropIndex(
                name: "IX_organization_enrollment_links_Status_LastChangedAtUtc",
                schema: "organizations",
                table: "organization_enrollment_links");
        }
    }
}
