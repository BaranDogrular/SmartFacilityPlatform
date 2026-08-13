using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFacility.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportIdempotencyFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FingerprintAlgorithm",
                schema: "ingestion",
                table: "ImportSourceRecords",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportSourceRecords_SourceSheet_FingerprintAlgorithm_IdempotencyFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords",
                columns: new[] { "SourceSheet", "FingerprintAlgorithm", "IdempotencyFingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportSourceRecords_SourceSheet_FingerprintAlgorithm_IdempotencyFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords");

            migrationBuilder.DropColumn(
                name: "FingerprintAlgorithm",
                schema: "ingestion",
                table: "ImportSourceRecords");

            migrationBuilder.DropColumn(
                name: "IdempotencyFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords");
        }
    }
}
