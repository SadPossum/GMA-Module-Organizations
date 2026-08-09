using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipMutationRetryProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastMutationKind",
                schema: "organizations",
                table: "organization_memberships",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMutationOperationId",
                schema: "organizations",
                table: "organization_memberships",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMutationKind",
                schema: "organizations",
                table: "organization_memberships");

            migrationBuilder.DropColumn(
                name: "LastMutationOperationId",
                schema: "organizations",
                table: "organization_memberships");
        }
    }
}
