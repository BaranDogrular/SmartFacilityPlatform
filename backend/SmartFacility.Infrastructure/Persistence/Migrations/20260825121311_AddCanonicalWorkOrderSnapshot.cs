using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFacility.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalWorkOrderSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalIdentityFingerprint",
                schema: "core",
                table: "WorkOrders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInCanonicalSnapshot",
                schema: "core",
                table: "WorkOrders",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "LastSeenImportBatchId",
                schema: "core",
                table: "WorkOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRowFingerprint",
                schema: "core",
                table: "WorkOrders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CanonicalIdentityFingerprint",
                schema: "core",
                table: "WorkOrders",
                column: "CanonicalIdentityFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_IsInCanonicalSnapshot",
                schema: "core",
                table: "WorkOrders",
                column: "IsInCanonicalSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_LastSeenImportBatchId",
                schema: "core",
                table: "WorkOrders",
                column: "LastSeenImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_RawStatusCode",
                schema: "core",
                table: "WorkOrders",
                column: "RawStatusCode");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_ImportBatches_LastSeenImportBatchId",
                schema: "core",
                table: "WorkOrders",
                column: "LastSeenImportBatchId",
                principalSchema: "ingestion",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_ImportBatches_LastSeenImportBatchId",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CanonicalIdentityFingerprint",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_IsInCanonicalSnapshot",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_LastSeenImportBatchId",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_RawStatusCode",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CanonicalIdentityFingerprint",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "IsInCanonicalSnapshot",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "LastSeenImportBatchId",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SourceRowFingerprint",
                schema: "core",
                table: "WorkOrders");
        }
    }
}
