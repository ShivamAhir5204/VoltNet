using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingRatesAndManagerRatePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "can_manage_rates",
                table: "StationManagers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChargingRates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    station_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    connector_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    rate_per_kwh = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    rate_per_hour = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingRates", x => x.id);
                    table.ForeignKey(
                        name: "FK_ChargingRates_Stations_station_id",
                        column: x => x.station_id,
                        principalTable: "Stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingRates_StationId",
                table: "ChargingRates",
                column: "station_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargingRates");

            migrationBuilder.DropColumn(
                name: "can_manage_rates",
                table: "StationManagers");
        }
    }
}
