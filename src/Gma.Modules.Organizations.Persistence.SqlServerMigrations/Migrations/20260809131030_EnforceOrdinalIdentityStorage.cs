using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOrdinalIdentityStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropSubjectIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organizations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organizations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                schema: "organizations",
                table: "organization_memberships",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_memberships",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organization_memberships",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "InviterSubjectId",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "AcceptedSubjectId",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "CreatorSubjectId",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                schema: "organizations",
                table: "organization_enrollment_claims",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_enrollment_claims",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192);

            CreateSubjectIndexes(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropSubjectIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organizations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organizations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                schema: "organizations",
                table: "organization_memberships",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_memberships",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organization_memberships",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "InviterSubjectId",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "AcceptedSubjectId",
                schema: "organizations",
                table: "organization_invitations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldNullable: true,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "CreatorSubjectId",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "organizations",
                table: "organization_enrollment_links",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                schema: "organizations",
                table: "organization_enrollment_claims",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "LastChangedBy",
                schema: "organizations",
                table: "organization_enrollment_claims",
                type: "nvarchar(192)",
                maxLength: 192,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(192)",
                oldMaxLength: 192,
                oldCollation: "Latin1_General_100_BIN2");

            CreateSubjectIndexes(migrationBuilder);
        }

        private static void DropSubjectIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organization_enrollment_claims_EnrollmentLinkId_SubjectId",
                schema: "organizations",
                table: "organization_enrollment_claims");

            migrationBuilder.DropIndex(
                name: "IX_organization_enrollment_claims_OrganizationId_SubjectId_Status_DecisionExpiresAtUtc",
                schema: "organizations",
                table: "organization_enrollment_claims");

            migrationBuilder.DropIndex(
                name: "IX_organization_memberships_OrganizationId_SubjectId",
                schema: "organizations",
                table: "organization_memberships");

            migrationBuilder.DropIndex(
                name: "IX_organization_memberships_SubjectId_Status_OrganizationId",
                schema: "organizations",
                table: "organization_memberships");
        }

        private static void CreateSubjectIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_claims_EnrollmentLinkId_SubjectId",
                schema: "organizations",
                table: "organization_enrollment_claims",
                columns: new[] { "EnrollmentLinkId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_claims_OrganizationId_SubjectId_Status_DecisionExpiresAtUtc",
                schema: "organizations",
                table: "organization_enrollment_claims",
                columns: new[]
                {
                    "OrganizationId",
                    "SubjectId",
                    "Status",
                    "DecisionExpiresAtUtc"
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_OrganizationId_SubjectId",
                schema: "organizations",
                table: "organization_memberships",
                columns: new[] { "OrganizationId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_SubjectId_Status_OrganizationId",
                schema: "organizations",
                table: "organization_memberships",
                columns: new[] { "SubjectId", "Status", "OrganizationId" });
        }
    }
}
