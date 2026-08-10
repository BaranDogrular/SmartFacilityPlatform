using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFacility.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportRowFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RowFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSourceRecords_SourceSheet_RowFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords",
                columns: new[] { "SourceSheet", "RowFingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportSourceRecords_SourceSheet_RowFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords");

            migrationBuilder.DropColumn(
                name: "RowFingerprint",
                schema: "ingestion",
                table: "ImportSourceRecords");
        }
    }
}
