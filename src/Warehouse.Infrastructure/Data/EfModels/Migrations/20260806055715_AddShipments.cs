using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Data.EfModels.Migrations
{
    /// <inheritdoc />
    public partial class AddShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DestinationLine1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DestinationCity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DestinationPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SupplierNotifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Shipments_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Shipments_SupplierId_fkey",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ShipmentLines_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "ShipmentLines_ProductId_fkey",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "ShipmentLines_ShipmentId_fkey",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ShipmentStatusChanges_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "ShipmentStatusChanges_ShipmentId_fkey",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_ProductId",
                table: "ShipmentLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ShipmentLines_ShipmentId_ProductId_key",
                table: "ShipmentLines",
                columns: new[] { "ShipmentId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_SupplierId",
                table: "Shipments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "Shipments_ReferenceNumber_key",
                table: "Shipments",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ShipmentStatusChanges_ShipmentId_OccurredAt_idx",
                table: "ShipmentStatusChanges",
                columns: new[] { "ShipmentId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentLines");

            migrationBuilder.DropTable(
                name: "ShipmentStatusChanges");

            migrationBuilder.DropTable(
                name: "Shipments");
        }
    }
}
