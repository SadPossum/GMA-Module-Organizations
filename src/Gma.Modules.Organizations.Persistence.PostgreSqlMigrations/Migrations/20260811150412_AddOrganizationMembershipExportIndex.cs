using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationMembershipExportIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_OrganizationId_Id",
                schema: "organizations",
                table: "organization_memberships",
                columns: new[] { "OrganizationId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organization_memberships_OrganizationId_Id",
                schema: "organizations",
                table: "organization_memberships");
        }
    }
}
