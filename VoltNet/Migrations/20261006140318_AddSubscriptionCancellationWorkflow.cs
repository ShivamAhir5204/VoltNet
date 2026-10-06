using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionCancellationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_admin_remarks",
                table: "OwnerSubscriptions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_processed_at",
                table: "OwnerSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "OwnerSubscriptions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_requested_at",
                table: "OwnerSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refund_amount",
                table: "OwnerSubscriptions",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_admin_remarks",
                table: "OwnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "cancellation_processed_at",
                table: "OwnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "OwnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_at",
                table: "OwnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "refund_amount",
                table: "OwnerSubscriptions");
        }
    }
}
