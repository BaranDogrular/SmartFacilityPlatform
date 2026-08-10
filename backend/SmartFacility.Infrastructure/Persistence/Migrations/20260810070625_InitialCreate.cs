using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFacility.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.EnsureSchema(
                name: "ingestion");

            migrationBuilder.CreateTable(
                name: "AssetGroups",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Buildings",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalWorkOrders",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReportedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Discipline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PersonnelName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BuildingNameRaw = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocationNameRaw = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolutionDurationRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalWorkOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SuccessfulRows = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FailedRows = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScadaAlarmEvents",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SectionRaw = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocationRaw = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FloorRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ZoneRaw = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AlarmType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    InterventionLevel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsibleRaw = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StatusRaw = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DateTimeParseStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScadaAlarmEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScadaOutages",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestoredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StatusRaw = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DateTimeParseStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScadaOutages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BuildingId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalSchema: "core",
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportErrors",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RawData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportErrors_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "ingestion",
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportSourceRecords",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    SourceSheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceRowNumber = table.Column<int>(type: "int", nullable: false),
                    RawData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawFormulaData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParseStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSourceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportSourceRecords_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "ingestion",
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    LastMaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ParentAssetId = table.Column<long>(type: "bigint", nullable: true),
                    BuildingId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    AssetGroupId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_AssetGroups_AssetGroupId",
                        column: x => x.AssetGroupId,
                        principalSchema: "core",
                        principalTable: "AssetGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assets_Assets_ParentAssetId",
                        column: x => x.ParentAssetId,
                        principalSchema: "core",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assets_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalSchema: "core",
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assets_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "core",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssetId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Discipline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AssignedPersonnelName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FailureType = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BuildingId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    ResponseDurationRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DowntimeRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaintenanceDurationRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalCostRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ServiceCostRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawStatusCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "core",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalSchema: "core",
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "core",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetCode",
                schema: "core",
                table: "Assets",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetGroupId",
                schema: "core",
                table: "Assets",
                column: "AssetGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_BuildingId",
                schema: "core",
                table: "Assets",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_LocationId",
                schema: "core",
                table: "Assets",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ParentAssetId",
                schema: "core",
                table: "Assets",
                column: "ParentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalWorkOrders_Discipline",
                schema: "analytics",
                table: "HistoricalWorkOrders",
                column: "Discipline");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalWorkOrders_ReportedDateTime",
                schema: "analytics",
                table: "HistoricalWorkOrders",
                column: "ReportedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_StartedAt",
                schema: "ingestion",
                table: "ImportBatches",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_Status",
                schema: "ingestion",
                table: "ImportBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImportErrors_ImportBatchId",
                schema: "ingestion",
                table: "ImportErrors",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSourceRecords_ImportBatchId_SourceSheet_SourceRowNumber",
                schema: "ingestion",
                table: "ImportSourceRecords",
                columns: new[] { "ImportBatchId", "SourceSheet", "SourceRowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_BuildingId",
                schema: "core",
                table: "Locations",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ScadaAlarmEvents_AlarmType",
                schema: "core",
                table: "ScadaAlarmEvents",
                column: "AlarmType");

            migrationBuilder.CreateIndex(
                name: "IX_ScadaAlarmEvents_ReceivedAt",
                schema: "core",
                table: "ScadaAlarmEvents",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScadaAlarmEvents_SourceSheet",
                schema: "core",
                table: "ScadaAlarmEvents",
                column: "SourceSheet");

            migrationBuilder.CreateIndex(
                name: "IX_ScadaAlarmEvents_StatusRaw",
                schema: "core",
                table: "ScadaAlarmEvents",
                column: "StatusRaw");

            migrationBuilder.CreateIndex(
                name: "IX_ScadaOutages_StartedAt",
                schema: "core",
                table: "ScadaOutages",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScadaOutages_StatusRaw",
                schema: "core",
                table: "ScadaOutages",
                column: "StatusRaw");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssetId",
                schema: "core",
                table: "WorkOrders",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_BuildingId",
                schema: "core",
                table: "WorkOrders",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_LocationId",
                schema: "core",
                table: "WorkOrders",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ReportedDateTime",
                schema: "core",
                table: "WorkOrders",
                column: "ReportedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_Status",
                schema: "core",
                table: "WorkOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkOrderNumber",
                schema: "core",
                table: "WorkOrders",
                column: "WorkOrderNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalWorkOrders",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "ImportErrors",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "ImportSourceRecords",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "ScadaAlarmEvents",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ScadaOutages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "WorkOrders",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ImportBatches",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "Assets",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AssetGroups",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Locations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Buildings",
                schema: "core");
        }
    }
}
