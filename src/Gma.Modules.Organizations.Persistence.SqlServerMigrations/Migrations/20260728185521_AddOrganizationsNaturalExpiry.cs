using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsNaturalExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionExpiresAtUtc",
                schema: "organizations",
                table: "organization_enrollment_claims",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [organizations].[organization_enrollment_claims]
                SET [DecisionExpiresAtUtc] = DATEADD(hour, 168, [CreatedAtUtc])
                WHERE [Status] = 1 AND [DecisionExpiresAtUtc] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_claims_Status_DecisionExpiresAtUtc",
                schema: "organizations",
                table: "organization_enrollment_claims",
                columns: new[] { "Status", "DecisionExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organization_enrollment_claims_Status_DecisionExpiresAtUtc",
                schema: "organizations",
                table: "organization_enrollment_claims");

            migrationBuilder.DropColumn(
                name: "DecisionExpiresAtUtc",
                schema: "organizations",
                table: "organization_enrollment_claims");
        }
    }
}
