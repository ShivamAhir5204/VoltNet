using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionLifecyclePaymentAndBookingFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "booking_fee",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 50.00m);

            migrationBuilder.AddColumn<DateTime>(
                name: "charging_started_at",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "Bookings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "Bookings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "booking_fee",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "charging_started_at",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "Bookings");
        }
    }
}
