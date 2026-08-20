using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltNet.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminUserRemoveMobileRenameToName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mobile",
                table: "AdminUsers");

            migrationBuilder.RenameColumn(
                name: "fullname",
                table: "AdminUsers",
                newName: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "AdminUsers",
                newName: "fullname");

            migrationBuilder.AddColumn<string>(
                name: "mobile",
                table: "AdminUsers",
                type: "varchar(15)",
                unicode: false,
                maxLength: 15,
                nullable: true);
        }
    }
}
