using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFacility.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveCanonicalWorkOrderRawKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetCodeRaw",
                schema: "core",
                table: "WorkOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationNameRaw",
                schema: "core",
                table: "WorkOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssetCodeRaw",
                schema: "core",
                table: "WorkOrders",
                column: "AssetCodeRaw");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_AssetCodeRaw",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "AssetCodeRaw",
                schema: "core",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "LocationNameRaw",
                schema: "core",
                table: "WorkOrders");
        }
    }
}
