using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PressingManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemServiceForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ServiceId",
                table: "OrderItems",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Services_ServiceId",
                table: "OrderItems",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Services_ServiceId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ServiceId",
                table: "OrderItems");
        }
    }
}
