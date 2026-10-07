using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class AddRealWorldChargingWaitlistAndBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "applied_rate_per_kwh",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_name",
                table: "Bookings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_phone",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "end_meter_reading",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_walk_in",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "meter_photo_url",
                table: "Bookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "start_meter_reading",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "units_consumed_kwh",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vehicle_model",
                table: "Bookings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vehicle_number_plate",
                table: "Bookings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StationBlockSlots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    station_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    charger_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    block_date = table.Column<DateTime>(type: "date", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    created_by_manager_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationBlockSlots", x => x.id);
                    table.ForeignKey(
                        name: "FK_StationBlockSlots_Chargers_charger_id",
                        column: x => x.charger_id,
                        principalTable: "Chargers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StationBlockSlots_Stations_station_id",
                        column: x => x.station_id,
                        principalTable: "Stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StationBlockSlots_UserMaster_created_by_manager_id",
                        column: x => x.created_by_manager_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    station_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    charger_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    booking_date = table.Column<DateTime>(type: "date", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    queue_position = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Waiting"),
                    vehicle_number_plate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    promoted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.id);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Chargers_charger_id",
                        column: x => x.charger_id,
                        principalTable: "Chargers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Stations_station_id",
                        column: x => x.station_id,
                        principalTable: "Stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_UserMaster_customer_id",
                        column: x => x.customer_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StationBlockSlots_charger_id",
                table: "StationBlockSlots",
                column: "charger_id");

            migrationBuilder.CreateIndex(
                name: "IX_StationBlockSlots_created_by_manager_id",
                table: "StationBlockSlots",
                column: "created_by_manager_id");

            migrationBuilder.CreateIndex(
                name: "IX_StationBlockSlots_Date",
                table: "StationBlockSlots",
                columns: new[] { "station_id", "block_date" });

            migrationBuilder.CreateIndex(
                name: "IX_Waitlist_Charger_Slot",
                table: "WaitlistEntries",
                columns: new[] { "charger_id", "booking_date", "start_time", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_customer_id",
                table: "WaitlistEntries",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_station_id",
                table: "WaitlistEntries",
                column: "station_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StationBlockSlots");

            migrationBuilder.DropTable(
                name: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "applied_rate_per_kwh",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "customer_name",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "customer_phone",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "end_meter_reading",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "is_walk_in",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "meter_photo_url",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "start_meter_reading",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "units_consumed_kwh",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "vehicle_model",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "vehicle_number_plate",
                table: "Bookings");
        }
    }
}
