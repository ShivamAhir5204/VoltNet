using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Subscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerSubscriptionId",
                table: "Stations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    price_per_month = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    max_stations = table.Column<int>(type: "int", nullable: false),
                    max_managers_per_station = table.Column<int>(type: "int", nullable: false),
                    duration_days = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OwnerSubscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    station_owner_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    plan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active"),
                    payment_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    amount_paid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerSubscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_OwnerSubscriptions_StationOwners_station_owner_id",
                        column: x => x.station_owner_id,
                        principalTable: "StationOwners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OwnerSubscriptions_SubscriptionPlans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stations_OwnerSubscriptionId",
                table: "Stations",
                column: "OwnerSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerSubscriptions_plan_id",
                table: "OwnerSubscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerSubscriptions_station_owner_id",
                table: "OwnerSubscriptions",
                column: "station_owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_OwnerSubscriptions_OwnerSubscriptionId",
                table: "Stations",
                column: "OwnerSubscriptionId",
                principalTable: "OwnerSubscriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stations_OwnerSubscriptions_OwnerSubscriptionId",
                table: "Stations");

            migrationBuilder.DropTable(
                name: "OwnerSubscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_Stations_OwnerSubscriptionId",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "OwnerSubscriptionId",
                table: "Stations");
        }
    }
}
