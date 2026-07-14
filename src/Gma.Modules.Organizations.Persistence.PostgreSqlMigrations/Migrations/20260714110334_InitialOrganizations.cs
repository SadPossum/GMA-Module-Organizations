using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organizations");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Handler = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.Id, x.Handler });
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActiveOwnerCount = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_enrollment_links",
                schema: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorSubjectId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TokenDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaximumClaims = table.Column<int>(type: "integer", nullable: false),
                    ReservedClaims = table.Column<int>(type: "integer", nullable: false),
                    ApprovalMode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_enrollment_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_enrollment_links_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organizations",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_invitations",
                schema: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterSubjectId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    TokenDigest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AcceptedSubjectId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AcceptedMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_invitations_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organizations",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                schema: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_memberships_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organizations",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_enrollment_claims",
                schema: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_enrollment_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_enrollment_claims_organization_enrollment_link~",
                        column: x => x.EnrollmentLinkId,
                        principalSchema: "organizations",
                        principalTable: "organization_enrollment_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_enrollment_claims_organization_memberships_Mem~",
                        column: x => x.MembershipId,
                        principalSchema: "organizations",
                        principalTable: "organization_memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_enrollment_claims_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organizations",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "organizations",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Status_ProcessedAtUtc",
                schema: "organizations",
                table: "inbox_messages",
                columns: new[] { "Status", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_claims_EnrollmentLinkId_SubjectId",
                schema: "organizations",
                table: "organization_enrollment_claims",
                columns: new[] { "EnrollmentLinkId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_claims_MembershipId",
                schema: "organizations",
                table: "organization_enrollment_claims",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_claims_OrganizationId_Status_Create~",
                schema: "organizations",
                table: "organization_enrollment_claims",
                columns: new[] { "OrganizationId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_links_OrganizationId_Status_Created~",
                schema: "organizations",
                table: "organization_enrollment_links",
                columns: new[] { "OrganizationId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_links_Status_ExpiresAtUtc",
                schema: "organizations",
                table: "organization_enrollment_links",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_enrollment_links_TokenDigest",
                schema: "organizations",
                table: "organization_enrollment_links",
                column: "TokenDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_OrganizationId_Status_CreatedAtUtc",
                schema: "organizations",
                table: "organization_invitations",
                columns: new[] { "OrganizationId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_Status_ExpiresAtUtc",
                schema: "organizations",
                table: "organization_invitations",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_TokenDigest",
                schema: "organizations",
                table: "organization_invitations",
                column: "TokenDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_OrganizationId_Status_Role",
                schema: "organizations",
                table: "organization_memberships",
                columns: new[] { "OrganizationId", "Status", "Role" });

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

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                schema: "organizations",
                table: "organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Status_Slug",
                schema: "organizations",
                table: "organizations",
                columns: new[] { "Status", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntil~",
                schema: "organizations",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "organization_enrollment_claims",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "organization_invitations",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "organization_enrollment_links",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "organization_memberships",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "organizations");
        }
    }
}
