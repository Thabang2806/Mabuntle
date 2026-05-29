using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerCartCheckoutStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE swyftly.carts AS c
                SET "Status" = 'CheckedOut'
                WHERE c."Status" = 'Active'
                  AND EXISTS (
                      SELECT 1
                      FROM swyftly.orders AS o
                      WHERE o."CartId" = c."Id"
                  );
                """);

            migrationBuilder.DropIndex(
                name: "IX_carts_BuyerId_Status",
                schema: "swyftly",
                table: "carts");

            migrationBuilder.CreateIndex(
                name: "IX_carts_BuyerId",
                schema: "swyftly",
                table: "carts",
                column: "BuyerId",
                unique: true,
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carts_BuyerId",
                schema: "swyftly",
                table: "carts");

            migrationBuilder.CreateIndex(
                name: "IX_carts_BuyerId_Status",
                schema: "swyftly",
                table: "carts",
                columns: new[] { "BuyerId", "Status" },
                unique: true);
        }
    }
}
