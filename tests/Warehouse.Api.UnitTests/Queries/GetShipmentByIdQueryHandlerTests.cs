using FluentAssertions;
using Warehouse.Application.Shipments.Queries;
using Warehouse.Domain;
using Warehouse.Infrastructure.Queries;
using static Warehouse.Api.UnitTests.Queries.ShipmentSeed;

namespace Warehouse.Api.UnitTests.Queries;

public class GetShipmentByIdQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetShipmentByIdQueryHandler CreateHandler() => new(Context);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ReturnsTheShipmentHeader()
    {
        var shipment = MakeShipment(reference: "SHP-0007", status: ShipmentStatus.Dispatched, trackingNumber: "TRK-1");
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(shipment.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ReferenceNumber.Should().Be("SHP-0007");
        result.Status.Should().Be(nameof(ShipmentStatus.Dispatched));
        result.TrackingNumber.Should().Be("TRK-1");
        result.SupplierName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task Handle_ProjectsTheDestinationColumnsIntoTheAddressViewModel()
    {
        var shipment = MakeShipment();
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(shipment.Id), CancellationToken.None);

        result!.Destination.Line1.Should().Be("12 Dock Road");
        result.Destination.City.Should().Be("Beirut");
        result.Destination.PostalCode.Should().Be("1107");
        result.Destination.Country.Should().Be("Lebanon");
    }

    [Fact]
    public async Task Handle_IncludesTheLinesInTheSameQuery()
    {
        var shipment = MakeShipment();
        await SeedAsync(
            shipment,
            MakeLine(shipment.Id, "Wireless Mouse", "SKU-001", 2),
            MakeLine(shipment.Id, "USB Cable", "SKU-002", 5));

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(shipment.Id), CancellationToken.None);

        result!.Lines.Should().HaveCount(2);
        result.Lines.Select(l => l.Sku).Should().BeEquivalentTo(new[] { "SKU-001", "SKU-002" });
    }

    [Fact]
    public async Task Handle_OrdersLinesByProductName()
    {
        var shipment = MakeShipment();
        await SeedAsync(
            shipment,
            MakeLine(shipment.Id, "Wireless Mouse", "SKU-001"),
            MakeLine(shipment.Id, "USB Cable", "SKU-002"),
            MakeLine(shipment.Id, "Adapter", "SKU-003"));

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(shipment.Id), CancellationToken.None);

        result!.Lines.Select(l => l.ProductName).Should().ContainInOrder("Adapter", "USB Cable", "Wireless Mouse");
    }

    [Fact]
    public async Task Handle_TotalUnits_IsTheSumOfTheLineQuantities()
    {
        var shipment = MakeShipment();
        await SeedAsync(
            shipment,
            MakeLine(shipment.Id, "Wireless Mouse", "SKU-001", 2),
            MakeLine(shipment.Id, "USB Cable", "SKU-002", 5));

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(shipment.Id), CancellationToken.None);

        result!.TotalUnits.Should().Be(7);
    }

    [Fact]
    public async Task Handle_ShipmentWithNoLines_ReportsZeroUnits()
    {
        var shipment = MakeShipment();
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(shipment.Id), CancellationToken.None);

        result!.Lines.Should().BeEmpty();
        result.TotalUnits.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DoesNotLeakLinesFromAnotherShipment()
    {
        var first = MakeShipment(reference: "SHP-0001");
        var second = MakeShipment(reference: "SHP-0002");
        await SeedAsync(
            first,
            second,
            MakeLine(first.Id, "Wireless Mouse", "SKU-001"),
            MakeLine(second.Id, "USB Cable", "SKU-002"));

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(first.Id), CancellationToken.None);

        result!.Lines.Should().ContainSingle().Which.Sku.Should().Be("SKU-001");
    }

    [Fact]
    public async Task Handle_CarriesTheLifecycleTimestamps()
    {
        var shipment = MakeShipment(
            status: ShipmentStatus.Delivered,
            dispatchedAt: new DateTime(2026, 3, 1),
            deliveredAt: new DateTime(2026, 3, 4),
            supplierNotifiedAt: new DateTime(2026, 3, 4, 1, 0, 0),
            expectedDeliveryDate: new DateTime(2026, 3, 5));
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(shipment.Id), CancellationToken.None);

        result!.DispatchedAt.Should().Be(new DateTime(2026, 3, 1));
        result.DeliveredAt.Should().Be(new DateTime(2026, 3, 4));
        result.SupplierNotifiedAt.Should().Be(new DateTime(2026, 3, 4, 1, 0, 0));
        result.ExpectedDeliveryDate.Should().Be(new DateTime(2026, 3, 5));
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_UnknownId_ReturnsNull()
    {
        await SeedAsync(MakeShipment());

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsNull()
    {
        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        await SeedAsync(MakeShipment());

        var result = await CreateHandler().Handle(new GetShipmentByIdQuery(Guid.Empty), CancellationToken.None);

        result.Should().BeNull();
    }
}
