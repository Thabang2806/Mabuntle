using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderAggregateFromCart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_reservations_carts_CartId",
                schema: "swyftly",
                table: "inventory_reservations");

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orders_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "swyftly",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "swyftly",
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "swyftly",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Size = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Colour = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "swyftly",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "swyftly",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "swyftly",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_status_history_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "swyftly",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                schema: "swyftly",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ProductId",
                schema: "swyftly",
                table: "order_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ProductVariantId",
                schema: "swyftly",
                table: "order_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_ChangedAtUtc",
                schema: "swyftly",
                table: "order_status_history",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_OrderId",
                schema: "swyftly",
                table: "order_status_history",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_BuyerId",
                schema: "swyftly",
                table: "orders",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_CartId",
                schema: "swyftly",
                table: "orders",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_CartId_BuyerId_Status",
                schema: "swyftly",
                table: "orders",
                columns: new[] { "CartId", "BuyerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatedAtUtc",
                schema: "swyftly",
                table: "orders",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_orders_SellerId",
                schema: "swyftly",
                table: "orders",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status",
                schema: "swyftly",
                table: "orders",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_reservations_carts_CartId",
                schema: "swyftly",
                table: "inventory_reservations",
                column: "CartId",
                principalSchema: "swyftly",
                principalTable: "carts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_reservations_carts_CartId",
                schema: "swyftly",
                table: "inventory_reservations");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "swyftly");

            migrationBuilder.DropTable(
                name: "order_status_history",
                schema: "swyftly");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "swyftly");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_reservations_carts_CartId",
                schema: "swyftly",
                table: "inventory_reservations",
                column: "CartId",
                principalSchema: "swyftly",
                principalTable: "carts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
