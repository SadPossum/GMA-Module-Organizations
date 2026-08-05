using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class HardenOrganizationScopeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_scope_states_closure",
                schema: "organizations",
                table: "organization_scope_states",
                sql: "(CAST(\"IsClosed\" AS integer) = 0 AND \"CloseOperationId\" IS NULL AND \"CloseRequestSha256\" IS NULL AND \"ClosedAtUtc\" IS NULL) OR (CAST(\"IsClosed\" AS integer) = 1 AND \"CloseOperationId\" IS NOT NULL AND \"CloseRequestSha256\" IS NOT NULL AND \"ClosedAtUtc\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_scope_destroy_receipts_progress",
                schema: "organizations",
                table: "organization_scope_destroy_receipts",
                sql: "\"BatchSize\" >= 1 AND \"BatchSize\" <= 1000 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"RemovalProofVersion\" >= 1 AND \"CompletedAtUtc\" >= \"StartedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_scope_destroy_receipts_revisions",
                schema: "organizations",
                table: "organization_scope_destroy_receipts",
                sql: "\"ExpectedRevision\" >= 0 AND \"ResultingRevision\" > \"ExpectedRevision\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_scope_destroy_operations_batch",
                schema: "organizations",
                table: "organization_scope_destroy_operations",
                sql: "\"BatchSize\" >= 1 AND \"BatchSize\" <= 1000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_scope_destroy_operations_progress",
                schema: "organizations",
                table: "organization_scope_destroy_operations",
                sql: "\"Stage\" >= 1 AND \"Stage\" <= 7 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" >= 1 AND \"UpdatedAtUtc\" >= \"StartedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_organization_scope_destroy_operations_revisions",
                schema: "organizations",
                table: "organization_scope_destroy_operations",
                sql: "\"ExpectedRevision\" >= 0 AND \"ResultingRevision\" > \"ExpectedRevision\"");

            migrationBuilder.AddForeignKey(
                name: "FK_organization_scope_destroy_operations_organization_scope_states_OrganizationId",
                schema: "organizations",
                table: "organization_scope_destroy_operations",
                column: "OrganizationId",
                principalSchema: "organizations",
                principalTable: "organization_scope_states",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_organization_scope_destroy_receipts_organization_scope_states_OrganizationId",
                schema: "organizations",
                table: "organization_scope_destroy_receipts",
                column: "OrganizationId",
                principalSchema: "organizations",
                principalTable: "organization_scope_states",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER
                    [organizations].[organization_scope_destroy_receipts_append_only]
                ON [organizations].[organization_scope_destroy_receipts]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000,
                        'organization scope destruction receipts are append-only',
                        1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER
                    [organizations].[organization_scope_states_closed_immutable]
                ON [organizations].[organization_scope_states]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM deleted
                        WHERE [IsClosed] = 1)
                    BEGIN
                        THROW 51000,
                            'closed organization scope state is immutable',
                            1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    [organizations].[organization_scope_destroy_receipts_append_only];
                """);

            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    [organizations].[organization_scope_states_closed_immutable];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_organization_scope_destroy_operations_organization_scope_states_OrganizationId",
                schema: "organizations",
                table: "organization_scope_destroy_operations");

            migrationBuilder.DropForeignKey(
                name: "FK_organization_scope_destroy_receipts_organization_scope_states_OrganizationId",
                schema: "organizations",
                table: "organization_scope_destroy_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_scope_states_closure",
                schema: "organizations",
                table: "organization_scope_states");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_scope_destroy_receipts_progress",
                schema: "organizations",
                table: "organization_scope_destroy_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_scope_destroy_receipts_revisions",
                schema: "organizations",
                table: "organization_scope_destroy_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_scope_destroy_operations_batch",
                schema: "organizations",
                table: "organization_scope_destroy_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_scope_destroy_operations_progress",
                schema: "organizations",
                table: "organization_scope_destroy_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_organization_scope_destroy_operations_revisions",
                schema: "organizations",
                table: "organization_scope_destroy_operations");
        }
    }
}
