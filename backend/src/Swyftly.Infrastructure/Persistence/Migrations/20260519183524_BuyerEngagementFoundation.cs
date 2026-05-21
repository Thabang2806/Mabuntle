using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerEngagementFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buyer_wishlist_items",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_wishlist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buyer_wishlist_items_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "swyftly",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_buyer_wishlist_items_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "swyftly",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalSchema: "swyftly",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_reviews",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_reviews_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "swyftly",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_reviews_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "swyftly",
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_reviews_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "swyftly",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_reviews_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "swyftly",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_reviews_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "swyftly",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_wishlist_items_BuyerId_ProductId",
                schema: "swyftly",
                table: "buyer_wishlist_items",
                columns: new[] { "BuyerId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_buyer_wishlist_items_CreatedAtUtc",
                schema: "swyftly",
                table: "buyer_wishlist_items",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_buyer_wishlist_items_ProductId",
                schema: "swyftly",
                table: "buyer_wishlist_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RecipientUserId_CreatedAtUtc",
                schema: "swyftly",
                table: "notifications",
                columns: new[] { "RecipientUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RecipientUserId_ReadAtUtc",
                schema: "swyftly",
                table: "notifications",
                columns: new[] { "RecipientUserId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_BuyerId",
                schema: "swyftly",
                table: "product_reviews",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_OrderId",
                schema: "swyftly",
                table: "product_reviews",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_OrderItemId",
                schema: "swyftly",
                table: "product_reviews",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_ProductId",
                schema: "swyftly",
                table: "product_reviews",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_ProductId_Status_CreatedAtUtc",
                schema: "swyftly",
                table: "product_reviews",
                columns: new[] { "ProductId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_SellerId",
                schema: "swyftly",
                table: "product_reviews",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buyer_wishlist_items",
                schema: "swyftly");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "swyftly");

            migrationBuilder.DropTable(
                name: "product_reviews",
                schema: "swyftly");
        }
    }
}
