using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class AdminUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "email",
                table: "AdminUsers",
                newName: "username");

            migrationBuilder.RenameIndex(
                name: "IX_AdminUsers_Email",
                table: "AdminUsers",
                newName: "IX_AdminUsers_Username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "username",
                table: "AdminUsers",
                newName: "email");

            migrationBuilder.RenameIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                newName: "IX_AdminUsers_Email");
        }
    }
}
