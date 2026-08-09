using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Organizations.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationMutationRetryProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastMutationKind",
                schema: "organizations",
                table: "organizations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMutationOperationId",
                schema: "organizations",
                table: "organizations",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMutationKind",
                schema: "organizations",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "LastMutationOperationId",
                schema: "organizations",
                table: "organizations");
        }
    }
}
