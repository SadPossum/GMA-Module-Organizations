using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddJoinSubjectPendingLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_claims_OrganizationId_SubjectId_Sta~",
                schema: "organizations",
                table: "organization_enrollment_claims",
                columns: new[] { "OrganizationId", "SubjectId", "Status", "DecisionExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organization_enrollment_claims_OrganizationId_SubjectId_Sta~",
                schema: "organizations",
                table: "organization_enrollment_claims");
        }
    }
}
