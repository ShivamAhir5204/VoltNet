using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    station_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    charger_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    booking_date = table.Column<DateTime>(type: "date", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "time", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Confirmed"),
                    estimated_cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    actual_cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    cancellation_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    payment_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.id);
                    table.ForeignKey(
                        name: "FK_Bookings_Chargers_charger_id",
                        column: x => x.charger_id,
                        principalTable: "Chargers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Stations_station_id",
                        column: x => x.station_id,
                        principalTable: "Stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_UserMaster_customer_id",
                        column: x => x.customer_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Charger_Date_Status",
                table: "Bookings",
                columns: new[] { "charger_id", "booking_date", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerId",
                table: "Bookings",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StationId",
                table: "Bookings",
                column: "station_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
