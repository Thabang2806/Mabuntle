using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "swyftly",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "swyftly",
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "swyftly",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_BuyerId",
                schema: "swyftly",
                table: "inventory_reservations",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_CartId",
                schema: "swyftly",
                table: "inventory_reservations",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ExpiresAtUtc",
                schema: "swyftly",
                table: "inventory_reservations",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ProductVariantId",
                schema: "swyftly",
                table: "inventory_reservations",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_Status",
                schema: "swyftly",
                table: "inventory_reservations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_reservations",
                schema: "swyftly");
        }
    }
}
