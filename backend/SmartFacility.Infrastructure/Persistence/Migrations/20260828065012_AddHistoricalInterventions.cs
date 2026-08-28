using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFacility.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalInterventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalInterventions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    SourceYear = table.Column<int>(type: "int", nullable: false),
                    SourceWorkOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssetCodeRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkOrderStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssetName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletionDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestDescriptionRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestDescriptionSanitized = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkPerformedDescriptionRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkPerformedDescriptionSanitized = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReasonDescriptionRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReasonDescriptionSanitized = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaintenanceDurationRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DowntimeDurationRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LaborDurationRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaterialCostRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LaborCostRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalCostRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalCostCurrencyRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InterventionQuality = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SourceRowFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FingerprintAlgorithm = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceSheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceRowNumber = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalInterventions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalInterventions_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "ingestion",
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalInterventions_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "core",
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalInterventions_ImportBatchId",
                schema: "core",
                table: "HistoricalInterventions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalInterventions_InterventionQuality_ReportedDateTime",
                schema: "core",
                table: "HistoricalInterventions",
                columns: new[] { "InterventionQuality", "ReportedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalInterventions_ReportedDateTime",
                schema: "core",
                table: "HistoricalInterventions",
                column: "ReportedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalInterventions_SourceRowFingerprint",
                schema: "core",
                table: "HistoricalInterventions",
                column: "SourceRowFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalInterventions_SourceYear",
                schema: "core",
                table: "HistoricalInterventions",
                column: "SourceYear");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalInterventions_WorkOrderId",
                schema: "core",
                table: "HistoricalInterventions",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalInterventions",
                schema: "core");
        }
    }
}
