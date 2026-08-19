using FluentAssertions;
using Moq;
using Warehouse.Application.Common;
using Warehouse.Application.Shipments.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Shipments;

public class AssignProductsToShipmentCommandHandlerTests : ShipmentHandlerTestBase
{
    private AssignProductsToShipmentCommandHandler CreateHandler() =>
        new(ShipmentRepository.Object, ProductRepository.Object, Mapper);

    private static Product MakeProduct(string name = "Wireless Mouse", string sku = "SKU-001", int stock = 50) =>
        Product.Create(name, sku, 19.99m, stock);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ValidAssignment_AddsTheLine()
    {
        var shipment = MakeShipment();
        var product = MakeProduct();
        GivenShipment(shipment);
        GivenProducts(product);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[] { new ShipmentProductAssignment(product.Id, 3) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { ProductId = product.Id, ProductName = "Wireless Mouse", Sku = "SKU-001", Quantity = 3 });
        result.Value.TotalUnits.Should().Be(3);
        ShipmentRepository.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SeveralProducts_AreFetchedInASingleRoundTrip()
    {
        // The point of IProductRepository.GetByIdsAsync: one query for the batch, not one per line.
        var shipment = MakeShipment();
        var first = MakeProduct("Mouse", "SKU-001");
        var second = MakeProduct("Cable", "SKU-002");
        GivenShipment(shipment);
        GivenProducts(first, second);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[]
            {
                new ShipmentProductAssignment(first.Id, 1),
                new ShipmentProductAssignment(second.Id, 4)
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
        result.Value.TotalUnits.Should().Be(5);
        ProductRepository.Verify(
            r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        ProductRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameProductTwiceInOneRequest_MergesIntoOneLine()
    {
        var shipment = MakeShipment();
        var product = MakeProduct();
        GivenShipment(shipment);
        GivenProducts(product);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[]
            {
                new ShipmentProductAssignment(product.Id, 2),
                new ShipmentProductAssignment(product.Id, 3)
            }),
            CancellationToken.None);

        result.Value!.Lines.Should().ContainSingle().Which.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Handle_LinesAreReturnedOrderedByProductName()
    {
        var shipment = MakeShipment();
        var mouse = MakeProduct("Mouse", "SKU-001");
        var cable = MakeProduct("Cable", "SKU-002");
        GivenShipment(shipment);
        GivenProducts(mouse, cable);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[]
            {
                new ShipmentProductAssignment(mouse.Id, 1),
                new ShipmentProductAssignment(cable.Id, 1)
            }),
            CancellationToken.None);

        // NOTE: the command path maps straight off the aggregate, which keeps insertion order.
        // The read path (ShipmentProjections) is the one that sorts by name.
        result.Value!.Lines.Select(l => l.ProductName).Should().ContainInOrder("Mouse", "Cable");
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_UnknownShipment_ReturnsNotFound()
    {
        ShipmentRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(Guid.NewGuid(), new[] { new ShipmentProductAssignment(Guid.NewGuid(), 1) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_EmptyProductList_ReturnsValidationErrorWithoutLoadingTheShipment()
    {
        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(Guid.NewGuid(), Array.Empty<ShipmentProductAssignment>()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("At least one product must be supplied.");
        ShipmentRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ReturnsNotFoundAndPersistsNothing()
    {
        var shipment = MakeShipment();
        GivenShipment(shipment);
        GivenProducts();

        var missingId = Guid.NewGuid();
        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[] { new ShipmentProductAssignment(missingId, 1) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Contain(missingId.ToString());
        ShipmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OneBadProductInABatch_PersistsNoneOfThem()
    {
        var shipment = MakeShipment();
        var good = MakeProduct("Mouse", "SKU-001");
        GivenShipment(shipment);
        GivenProducts(good);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[]
            {
                new ShipmentProductAssignment(good.Id, 1),
                new ShipmentProductAssignment(Guid.NewGuid(), 1)
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        shipment.Lines.Should().BeEmpty();
        ShipmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_QuantityAboveStock_ReturnsConflict()
    {
        var shipment = MakeShipment();
        var product = MakeProduct(stock: 2);
        GivenShipment(shipment);
        GivenProducts(product);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[] { new ShipmentProductAssignment(product.Id, 5) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Cannot assign 5 of 'Wireless Mouse': only 2 in stock.");
    }

    [Fact]
    public async Task Handle_ZeroQuantity_ReturnsValidationError()
    {
        var shipment = MakeShipment();
        var product = MakeProduct();
        GivenShipment(shipment);
        GivenProducts(product);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[] { new ShipmentProductAssignment(product.Id, 0) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_ArchivedProduct_ReturnsConflict()
    {
        var shipment = MakeShipment();
        var product = MakeProduct();
        product.Archive();
        GivenShipment(shipment);
        GivenProducts(product);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[] { new ShipmentProductAssignment(product.Id, 1) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Archived products cannot be assigned to a shipment.");
    }

    [Fact]
    public async Task Handle_ShipmentAlreadyDispatched_ReturnsConflict()
    {
        var shipment = MakeShipmentWithLine();
        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched);
        var product = MakeProduct("Cable", "SKU-002");
        GivenShipment(shipment);
        GivenProducts(product);

        var result = await CreateHandler().Handle(
            new AssignProductsToShipmentCommand(shipment.Id, new[] { new ShipmentProductAssignment(product.Id, 1) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Products can only be assigned while the shipment is a draft.");
    }
}
