using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class AddStationOwnerAndUpdateStationManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StationManagers_user_id",
                table: "StationManagers");

            migrationBuilder.RenameColumn(
                name: "assigned_at",
                table: "StationManagers",
                newName: "created_at");

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "Stations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "StationManagers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "StationManagers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "StationManagers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "StationManagers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "StationOwners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    business_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    business_registration_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    gst_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationOwners", x => x.id);
                    table.ForeignKey(
                        name: "FK_StationOwners_UserMaster_approved_by",
                        column: x => x.approved_by,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StationOwners_UserMaster_user_id",
                        column: x => x.user_id,
                        principalTable: "UserMaster",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StationManagers_created_by",
                table: "StationManagers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_StationManagers_UserId",
                table: "StationManagers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationOwners_approved_by",
                table: "StationOwners",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_StationOwners_UserId",
                table: "StationOwners",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StationManagers_StationOwners_created_by",
                table: "StationManagers",
                column: "created_by",
                principalTable: "StationOwners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StationManagers_StationOwners_created_by",
                table: "StationManagers");

            migrationBuilder.DropTable(
                name: "StationOwners");

            migrationBuilder.DropIndex(
                name: "IX_StationManagers_created_by",
                table: "StationManagers");

            migrationBuilder.DropIndex(
                name: "IX_StationManagers_UserId",
                table: "StationManagers");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "StationManagers");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "StationManagers");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "StationManagers");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "StationManagers");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "StationManagers",
                newName: "assigned_at");

            migrationBuilder.CreateIndex(
                name: "IX_StationManagers_user_id",
                table: "StationManagers",
                column: "user_id");
        }
    }
}
