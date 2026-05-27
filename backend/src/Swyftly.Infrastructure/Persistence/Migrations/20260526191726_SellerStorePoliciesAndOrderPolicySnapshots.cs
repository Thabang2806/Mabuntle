using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerStorePoliciesAndOrderPolicySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyCareInstructions",
                schema: "swyftly",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyExchangePolicy",
                schema: "swyftly",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyFulfilmentPolicy",
                schema: "swyftly",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyProductDisclaimer",
                schema: "swyftly",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyReturnPolicy",
                schema: "swyftly",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SellerPolicyReturnWindowDays",
                schema: "swyftly",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SellerPolicySnapshotAtUtc",
                schema: "swyftly",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicySupportPolicy",
                schema: "swyftly",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seller_store_policies",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnWindowDays = table.Column<int>(type: "integer", nullable: true),
                    ReturnPolicy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExchangePolicy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FulfilmentPolicy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupportPolicy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CareInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProductDisclaimer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_store_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_store_policies_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "swyftly",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_store_policies_SellerId",
                schema: "swyftly",
                table: "seller_store_policies",
                column: "SellerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_store_policies",
                schema: "swyftly");

            migrationBuilder.DropColumn(
                name: "SellerPolicyCareInstructions",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyExchangePolicy",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyFulfilmentPolicy",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyProductDisclaimer",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyReturnPolicy",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyReturnWindowDays",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicySnapshotAtUtc",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicySupportPolicy",
                schema: "swyftly",
                table: "orders");
        }
    }
}
