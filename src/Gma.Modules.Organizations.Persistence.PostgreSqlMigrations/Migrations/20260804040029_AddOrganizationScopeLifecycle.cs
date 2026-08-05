using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationScopeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_scope_destroy_operations",
                schema: "organizations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpectedRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingRevision = table.Column<long>(type: "bigint", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    RemovedRecordCount = table.Column<long>(type: "bigint", nullable: false),
                    CompletedBatchCount = table.Column<int>(type: "integer", nullable: false),
                    ProofVersion = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_scope_destroy_operations", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "organization_scope_destroy_receipts",
                schema: "organizations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpectedRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingRevision = table.Column<long>(type: "bigint", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    RemovedRecordCount = table.Column<long>(type: "bigint", nullable: false),
                    CompletedBatchCount = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofVersion = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_scope_destroy_receipts", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "organization_scope_states",
                schema: "organizations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    CloseOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CloseRequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_scope_states", x => x.OrganizationId);
                    table.CheckConstraint("CK_organization_scope_states_version", "\"Version\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_scope_destroy_operations_OperationId",
                schema: "organizations",
                table: "organization_scope_destroy_operations",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_scope_destroy_receipts_OperationId",
                schema: "organizations",
                table: "organization_scope_destroy_receipts",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_scope_states_IsClosed_ClosedAtUtc_Organization~",
                schema: "organizations",
                table: "organization_scope_states",
                columns: new[] { "IsClosed", "ClosedAtUtc", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_scope_states_ScopeId",
                schema: "organizations",
                table: "organization_scope_states",
                column: "ScopeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_scope_destroy_operations",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "organization_scope_destroy_receipts",
                schema: "organizations");

            migrationBuilder.DropTable(
                name: "organization_scope_states",
                schema: "organizations");
        }
    }
}
