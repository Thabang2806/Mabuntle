using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportTicketSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_tickets",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    LinkedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedSellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedSupportUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_support_tickets_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "swyftly",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_orders_LinkedOrderId",
                        column: x => x.LinkedOrderId,
                        principalSchema: "swyftly",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_payments_LinkedPaymentId",
                        column: x => x.LinkedPaymentId,
                        principalSchema: "swyftly",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_products_LinkedProductId",
                        column: x => x.LinkedProductId,
                        principalSchema: "swyftly",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_seller_profiles_LinkedSellerId",
                        column: x => x.LinkedSellerId,
                        principalSchema: "swyftly",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "swyftly",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "support_messages",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_support_messages_support_tickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalSchema: "swyftly",
                        principalTable: "support_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_CreatedAtUtc",
                schema: "swyftly",
                table: "support_messages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_IsInternal",
                schema: "swyftly",
                table: "support_messages",
                column: "IsInternal");

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_SenderUserId",
                schema: "swyftly",
                table: "support_messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_SupportTicketId",
                schema: "swyftly",
                table: "support_messages",
                column: "SupportTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_BuyerId",
                schema: "swyftly",
                table: "support_tickets",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_Category",
                schema: "swyftly",
                table: "support_tickets",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_CreatedByUserId",
                schema: "swyftly",
                table: "support_tickets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedOrderId",
                schema: "swyftly",
                table: "support_tickets",
                column: "LinkedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedPaymentId",
                schema: "swyftly",
                table: "support_tickets",
                column: "LinkedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedProductId",
                schema: "swyftly",
                table: "support_tickets",
                column: "LinkedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedSellerId",
                schema: "swyftly",
                table: "support_tickets",
                column: "LinkedSellerId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_OpenedAtUtc",
                schema: "swyftly",
                table: "support_tickets",
                column: "OpenedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_SellerId",
                schema: "swyftly",
                table: "support_tickets",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_Status",
                schema: "swyftly",
                table: "support_tickets",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_messages",
                schema: "swyftly");

            migrationBuilder.DropTable(
                name: "support_tickets",
                schema: "swyftly");
        }
    }
}
