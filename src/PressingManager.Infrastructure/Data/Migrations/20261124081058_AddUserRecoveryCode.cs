using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PressingManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRecoveryCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryCodeHash",
                table: "Users",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryCodeHash",
                table: "Users");
        }
    }
}
