using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PressingManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingInventoryRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_UserId",
                table: "StockMovements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_ChangedByUserId",
                table: "OrderStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStatusHistory_Orders_OrderId",
                table: "OrderStatusHistory",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStatusHistory_Users_ChangedByUserId",
                table: "OrderStatusHistory",
                column: "ChangedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Users_UserId",
                table: "StockMovements",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderStatusHistory_Orders_OrderId",
                table: "OrderStatusHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderStatusHistory_Users_ChangedByUserId",
                table: "OrderStatusHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Users_UserId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_UserId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistory_ChangedByUserId",
                table: "OrderStatusHistory");
        }
    }
}
