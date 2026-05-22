using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddressVerificationAndPickupPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryVerificationProvider",
                schema: "swyftly",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryVerificationStatus",
                schema: "swyftly",
                table: "orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unverified");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryVerificationWarningsJson",
                schema: "swyftly",
                table: "orders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveryVerifiedAtUtc",
                schema: "swyftly",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointAddressLine1",
                schema: "swyftly",
                table: "orders",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointAddressLine2",
                schema: "swyftly",
                table: "orders",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointCity",
                schema: "swyftly",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointCode",
                schema: "swyftly",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointCountryCode",
                schema: "swyftly",
                table: "orders",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupPointId",
                schema: "swyftly",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupPointLatitude",
                schema: "swyftly",
                table: "orders",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupPointLongitude",
                schema: "swyftly",
                table: "orders",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointName",
                schema: "swyftly",
                table: "orders",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointOpeningHours",
                schema: "swyftly",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointPostalCode",
                schema: "swyftly",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointProviderName",
                schema: "swyftly",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointProvince",
                schema: "swyftly",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupPointSuburb",
                schema: "swyftly",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationProvider",
                schema: "swyftly",
                table: "buyer_delivery_addresses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                schema: "swyftly",
                table: "buyer_delivery_addresses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unverified");

            migrationBuilder.AddColumn<string>(
                name: "VerificationWarningsJson",
                schema: "swyftly",
                table: "buyer_delivery_addresses",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAtUtc",
                schema: "swyftly",
                table: "buyer_delivery_addresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pickup_points",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Suburb = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Province = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    OpeningHours = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pickup_points", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_PickupPointId",
                schema: "swyftly",
                table: "orders",
                column: "PickupPointId");

            migrationBuilder.CreateIndex(
                name: "IX_pickup_points_CountryCode_Province_IsActive",
                schema: "swyftly",
                table: "pickup_points",
                columns: new[] { "CountryCode", "Province", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_pickup_points_ProviderName_Code",
                schema: "swyftly",
                table: "pickup_points",
                columns: new[] { "ProviderName", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pickup_points",
                schema: "swyftly");

            migrationBuilder.DropIndex(
                name: "IX_orders_PickupPointId",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryVerificationProvider",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryVerificationStatus",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryVerificationWarningsJson",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryVerifiedAtUtc",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointAddressLine1",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointAddressLine2",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointCity",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointCode",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointCountryCode",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointId",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointLatitude",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointLongitude",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointName",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointOpeningHours",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointPostalCode",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointProviderName",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointProvince",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PickupPointSuburb",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "VerificationProvider",
                schema: "swyftly",
                table: "buyer_delivery_addresses");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                schema: "swyftly",
                table: "buyer_delivery_addresses");

            migrationBuilder.DropColumn(
                name: "VerificationWarningsJson",
                schema: "swyftly",
                table: "buyer_delivery_addresses");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                schema: "swyftly",
                table: "buyer_delivery_addresses");
        }
    }
}
