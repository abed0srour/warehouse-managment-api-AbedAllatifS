using FluentAssertions;
using Warehouse.Application.Shipments.Queries;
using Warehouse.Domain;
using Warehouse.Infrastructure.Queries;
using static Warehouse.Api.UnitTests.Queries.ShipmentSeed;

namespace Warehouse.Api.UnitTests.Queries;

public class GetShipmentStatusQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetShipmentStatusQueryHandler CreateHandler() => new(Context);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ReturnsTheCurrentState()
    {
        var shipment = MakeShipment(
            reference: "SHP-0007", status: ShipmentStatus.InTransit, trackingNumber: "TRK-1");
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(shipment.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ReferenceNumber.Should().Be("SHP-0007");
        result.Status.Should().Be(nameof(ShipmentStatus.InTransit));
        result.TrackingNumber.Should().Be("TRK-1");
        result.SupplierName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task Handle_ReturnsTheTransitionHistoryOldestFirst()
    {
        var shipment = MakeShipment(status: ShipmentStatus.Delivered);
        await SeedAsync(
            shipment,
            MakeStatusChange(shipment.Id, ShipmentStatus.Dispatched, ShipmentStatus.Delivered, new DateTime(2026, 3, 4)),
            MakeStatusChange(shipment.Id, ShipmentStatus.Draft, ShipmentStatus.Dispatched, new DateTime(2026, 3, 1)));

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(shipment.Id), CancellationToken.None);

        result!.History.Select(h => h.ToStatus)
            .Should().ContainInOrder(nameof(ShipmentStatus.Dispatched), nameof(ShipmentStatus.Delivered));
    }

    [Fact]
    public async Task Handle_HistoryCarriesTheNoteAndTimestamp()
    {
        var shipment = MakeShipment(status: ShipmentStatus.Dispatched);
        await SeedAsync(
            shipment,
            MakeStatusChange(
                shipment.Id, ShipmentStatus.Draft, ShipmentStatus.Dispatched,
                new DateTime(2026, 3, 1, 9, 30, 0), "left the dock"));

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(shipment.Id), CancellationToken.None);

        var entry = result!.History.Should().ContainSingle().Subject;
        entry.FromStatus.Should().Be(nameof(ShipmentStatus.Draft));
        entry.Note.Should().Be("left the dock");
        entry.OccurredAt.Should().Be(new DateTime(2026, 3, 1, 9, 30, 0));
    }

    [Fact]
    public async Task Handle_DraftShipment_HasNoHistoryYet()
    {
        var shipment = MakeShipment();
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(shipment.Id), CancellationToken.None);

        result!.History.Should().BeEmpty();
        result.IsClosed.Should().BeFalse();
    }

    [Theory]
    [InlineData(ShipmentStatus.Delivered, true)]
    [InlineData(ShipmentStatus.Cancelled, true)]
    [InlineData(ShipmentStatus.Draft, false)]
    [InlineData(ShipmentStatus.Dispatched, false)]
    [InlineData(ShipmentStatus.InTransit, false)]
    public async Task Handle_IsClosed_IsTrueOnlyForTerminalStates(ShipmentStatus status, bool expected)
    {
        var shipment = MakeShipment(status: status);
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(shipment.Id), CancellationToken.None);

        result!.IsClosed.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_DoesNotLeakHistoryFromAnotherShipment()
    {
        var first = MakeShipment(reference: "SHP-0001", status: ShipmentStatus.Dispatched);
        var second = MakeShipment(reference: "SHP-0002", status: ShipmentStatus.Dispatched);
        await SeedAsync(
            first,
            second,
            MakeStatusChange(first.Id, ShipmentStatus.Draft, ShipmentStatus.Dispatched, new DateTime(2026, 3, 1), "first"),
            MakeStatusChange(second.Id, ShipmentStatus.Draft, ShipmentStatus.Dispatched, new DateTime(2026, 3, 2), "second"));

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(first.Id), CancellationToken.None);

        result!.History.Should().ContainSingle().Which.Note.Should().Be("first");
    }

    [Fact]
    public async Task Handle_ReportsWhenTheSupplierWasLastNotified()
    {
        var shipment = MakeShipment(
            status: ShipmentStatus.Dispatched, supplierNotifiedAt: new DateTime(2026, 3, 1, 10, 0, 0));
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(shipment.Id), CancellationToken.None);

        result!.SupplierNotifiedAt.Should().Be(new DateTime(2026, 3, 1, 10, 0, 0));
    }

    [Fact]
    public async Task Handle_NeverNotified_LeavesTheStampNull()
    {
        var shipment = MakeShipment();
        await SeedAsync(shipment);

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(shipment.Id), CancellationToken.None);

        result!.SupplierNotifiedAt.Should().BeNull();
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_UnknownId_ReturnsNull()
    {
        await SeedAsync(MakeShipment());

        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsNull()
    {
        var result = await CreateHandler().Handle(new GetShipmentStatusQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}
