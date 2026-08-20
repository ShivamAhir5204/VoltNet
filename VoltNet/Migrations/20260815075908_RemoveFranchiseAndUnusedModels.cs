using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFranchiseAndUnusedModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stations_FranchiseMaster_franchise_id",
                table: "Stations");

            migrationBuilder.DropTable(
                name: "FranchiseSettlements");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "MaintenanceTickets");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "FranchiseMaster");

            migrationBuilder.DropTable(
                name: "ChargingSessions");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Chargers");

            migrationBuilder.RenameColumn(
                name: "franchise_id",
                table: "Stations",
                newName: "owner_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_Stations_franchise_id",
                table: "Stations",
                newName: "IX_Stations_owner_user_id");

            migrationBuilder.Sql("UPDATE [Stations] SET owner_user_id = NULL;");

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_UserMaster_owner_user_id",
                table: "Stations",
                column: "owner_user_id",
                principalTable: "UserMaster",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stations_UserMaster_owner_user_id",
                table: "Stations");

            migrationBuilder.RenameColumn(
                name: "owner_user_id",
                table: "Stations",
                newName: "franchise_id");

            migrationBuilder.RenameIndex(
                name: "IX_Stations_owner_user_id",
                table: "Stations",
                newName: "IX_Stations_franchise_id");

            migrationBuilder.CreateTable(
                name: "Chargers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    station_id = table.Column<int>(type: "int", nullable: false),
                    charger_code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    connector_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    last_serviced_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    power_rating_kw = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    rate_per_kwh = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chargers", x => x.id);
                    table.ForeignKey(
                        name: "FK_Chargers_Stations_station_id",
                        column: x => x.station_id,
                        principalTable: "Stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseMaster",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    contact_email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    franchise_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    revenue_share_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseMaster", x => x.id);
                    table.ForeignKey(
                        name: "FK_FranchiseMaster_UserMaster_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    balance = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    last_updated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.id);
                    table.ForeignKey(
                        name: "FK_Wallets_UserMaster_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    charger_id = table.Column<int>(type: "int", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    booking_time = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    slot_end_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    slot_start_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
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
                        name: "FK_Bookings_UserMaster_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTickets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    assigned_technician_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    charger_id = table.Column<int>(type: "int", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    issue_description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    reported_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Open")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTickets", x => x.id);
                    table.ForeignKey(
                        name: "FK_MaintenanceTickets_Chargers_charger_id",
                        column: x => x.charger_id,
                        principalTable: "Chargers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTickets_UserMaster_assigned_technician_id",
                        column: x => x.assigned_technician_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTickets_UserMaster_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FranchiseSettlements",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    franchise_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    company_share_amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    franchise_share_amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    period_end = table.Column<DateTime>(type: "datetime2", nullable: false),
                    period_start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    settlement_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Pending"),
                    total_revenue = table.Column<decimal>(type: "decimal(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseSettlements", x => x.id);
                    table.ForeignKey(
                        name: "FK_FranchiseSettlements_FranchiseMaster_franchise_id",
                        column: x => x.franchise_id,
                        principalTable: "FranchiseMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    wallet_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "Wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChargingSessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    booking_id = table.Column<int>(type: "int", nullable: false),
                    actual_end_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    actual_start_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    cost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    energy_consumed_kwh = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    payment_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingSessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ChargingSessions_Bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "Bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    charging_session_id = table.Column<int>(type: "int", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    gst_amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    issued_date = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    payment_method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_Invoices_ChargingSessions_charging_session_id",
                        column: x => x.charging_session_id,
                        principalTable: "ChargingSessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_UserMaster_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ChargerId_SlotStartTime_SlotEndTime",
                table: "Bookings",
                columns: new[] { "charger_id", "slot_start_time", "slot_end_time" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerUserId",
                table: "Bookings",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Chargers_ChargerCode",
                table: "Chargers",
                column: "charger_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chargers_StationId",
                table: "Chargers",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_Chargers_Status",
                table: "Chargers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingSessions_BookingId",
                table: "ChargingSessions",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMaster_owner_user_id",
                table: "FranchiseMaster",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSettlements_FranchiseId_Period",
                table: "FranchiseSettlements",
                columns: new[] { "franchise_id", "period_start", "period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ChargingSessionId",
                table: "Invoices",
                column: "charging_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_customer_user_id",
                table: "Invoices",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_AssignedTechnicianId",
                table: "MaintenanceTickets",
                column: "assigned_technician_id");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_ChargerId",
                table: "MaintenanceTickets",
                column: "charger_id");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_reported_by_user_id",
                table: "MaintenanceTickets",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_Status",
                table: "MaintenanceTickets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CustomerUserId",
                table: "Wallets",
                column: "customer_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId",
                table: "WalletTransactions",
                column: "wallet_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_FranchiseMaster_franchise_id",
                table: "Stations",
                column: "franchise_id",
                principalTable: "FranchiseMaster",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
