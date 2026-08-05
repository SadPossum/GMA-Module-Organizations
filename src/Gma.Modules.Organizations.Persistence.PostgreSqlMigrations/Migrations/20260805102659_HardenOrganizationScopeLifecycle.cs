using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.PostgreSqlMigrations.Migrations
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
                name: "FK_organization_scope_destroy_operations_organization_scope_st~",
                schema: "organizations",
                table: "organization_scope_destroy_operations",
                column: "OrganizationId",
                principalSchema: "organizations",
                principalTable: "organization_scope_states",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_organization_scope_destroy_receipts_organization_scope_stat~",
                schema: "organizations",
                table: "organization_scope_destroy_receipts",
                column: "OrganizationId",
                principalSchema: "organizations",
                principalTable: "organization_scope_states",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION
                    organizations.reject_organization_scope_destroy_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'organization scope destruction receipts are append-only';
                END;
                $$;

                CREATE TRIGGER organization_scope_destroy_receipts_append_only
                BEFORE UPDATE OR DELETE
                ON organizations.organization_scope_destroy_receipts
                FOR EACH ROW
                EXECUTE FUNCTION
                    organizations.reject_organization_scope_destroy_receipt_mutation();

                CREATE FUNCTION
                    organizations.reject_closed_organization_scope_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF OLD."IsClosed" THEN
                        RAISE EXCEPTION
                            'closed organization scope state is immutable';
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER organization_scope_states_closed_immutable
                BEFORE UPDATE OR DELETE
                ON organizations.organization_scope_states
                FOR EACH ROW
                EXECUTE FUNCTION
                    organizations.reject_closed_organization_scope_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    organization_scope_destroy_receipts_append_only
                ON organizations.organization_scope_destroy_receipts;

                DROP FUNCTION IF EXISTS
                    organizations.reject_organization_scope_destroy_receipt_mutation();

                DROP TRIGGER IF EXISTS
                    organization_scope_states_closed_immutable
                ON organizations.organization_scope_states;

                DROP FUNCTION IF EXISTS
                    organizations.reject_closed_organization_scope_mutation();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_organization_scope_destroy_operations_organization_scope_st~",
                schema: "organizations",
                table: "organization_scope_destroy_operations");

            migrationBuilder.DropForeignKey(
                name: "FK_organization_scope_destroy_receipts_organization_scope_stat~",
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
