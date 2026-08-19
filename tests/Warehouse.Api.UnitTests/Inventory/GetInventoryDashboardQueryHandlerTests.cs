using FluentAssertions;
using Moq;
using Warehouse.Application.Inventory.Queries;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Inventory;

public class GetInventoryDashboardQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly GetInventoryDashboardQueryHandler _handler;

    public GetInventoryDashboardQueryHandlerTests()
    {
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Product>());
        _supplierRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Supplier>());

        _handler = new GetInventoryDashboardQueryHandler(_productRepository.Object, _supplierRepository.Object);
    }

    private static Product MakeProduct(string name, int quantity, bool archived = false)
    {
        var product = Product.Create(name, $"SKU-{Guid.NewGuid():N}".Substring(0, 12), 10m, quantity);
        if (archived)
            product.Archive();
        return product;
    }

    private void GivenProducts(params Product[] products) =>
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

    private void GivenSuppliers(params Supplier[] suppliers) =>
        _supplierRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(suppliers);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ReturnsTotalProductCount()
    {
        GivenProducts(MakeProduct("A", 50), MakeProduct("B", 50), MakeProduct("C", 50));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.TotalProducts.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ReturnsTotalSupplierCount()
    {
        GivenSuppliers(new Supplier { Name = "Acme" }, new Supplier { Name = "Globex" });

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.TotalSuppliers.Should().Be(2);
    }

    [Fact]
    public async Task Handle_IdentifiesLowStockProducts()
    {
        GivenProducts(MakeProduct("Low", 3), MakeProduct("Healthy", 500));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.LowStockProducts.Should().ContainSingle().Which.Name.Should().Be("Low");
    }

    [Fact]
    public async Task Handle_LowStockDtoCarriesIdNameAndQuantity()
    {
        var product = MakeProduct("Low", 3);
        GivenProducts(product);

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        var dto = result.LowStockProducts.Single();
        dto.Id.Should().Be(product.Id);
        dto.Name.Should().Be("Low");
        dto.QuantityInStock.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CustomThreshold_IsHonoured()
    {
        GivenProducts(MakeProduct("Twenty", 20), MakeProduct("Sixty", 60));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(LowStockThreshold: 50), CancellationToken.None);

        result.LowStockProducts.Should().ContainSingle().Which.Name.Should().Be("Twenty");
    }

    [Fact]
    public async Task Handle_ArchivedProducts_ExcludedFromLowStockButCountedInTotal()
    {
        GivenProducts(MakeProduct("ArchivedLow", 1, archived: true), MakeProduct("ActiveLow", 1));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.TotalProducts.Should().Be(2);
        result.LowStockProducts.Should().ContainSingle().Which.Name.Should().Be("ActiveLow");
    }

    // ---------- boundary ----------

    [Fact]
    public async Task Handle_QuantityExactlyAtThreshold_IsNotLowStock()
    {
        // The predicate is strict "<", so a product sitting exactly on the threshold is healthy.
        GivenProducts(MakeProduct("Exactly10", 10));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(LowStockThreshold: 10), CancellationToken.None);

        result.LowStockProducts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_QuantityOneBelowThreshold_IsLowStock()
    {
        GivenProducts(MakeProduct("Nine", 9));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(LowStockThreshold: 10), CancellationToken.None);

        result.LowStockProducts.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ZeroQuantity_IsLowStock()
    {
        GivenProducts(MakeProduct("OutOfStock", 0));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.LowStockProducts.Should().ContainSingle().Which.QuantityInStock.Should().Be(0);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_NoData_ReturnsZeroedDashboard()
    {
        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.TotalProducts.Should().Be(0);
        result.TotalSuppliers.Should().Be(0);
        result.LowStockProducts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProductRepositoryThrows_PropagatesException()
    {
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var act = async () => await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
    }

    [Fact]
    public async Task Handle_SupplierRepositoryThrows_PropagatesException()
    {
        _supplierRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("supplier store unavailable"));

        var act = async () => await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("supplier store unavailable");
    }

    [Fact]
    public async Task Handle_NegativeThreshold_ReturnsNoLowStockProducts()
    {
        // No validation exists on LowStockThreshold. A negative threshold silently yields
        // an empty low-stock list rather than being rejected.
        GivenProducts(MakeProduct("OutOfStock", 0), MakeProduct("Low", 1));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(LowStockThreshold: -1), CancellationToken.None);

        result.LowStockProducts.Should().BeEmpty();
        result.TotalProducts.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ZeroThreshold_ReturnsNoLowStockProducts()
    {
        GivenProducts(MakeProduct("OutOfStock", 0));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(LowStockThreshold: 0), CancellationToken.None);

        result.LowStockProducts.Should().BeEmpty();
    }

    // ---------- edge cases ----------

    [Fact]
    public async Task Handle_MaxIntThreshold_FlagsEveryActiveProductWithoutOverflow()
    {
        GivenProducts(MakeProduct("Bulk", int.MaxValue - 1), MakeProduct("Normal", 5));

        var result = await _handler.Handle(
            new GetInventoryDashboardQuery(LowStockThreshold: int.MaxValue),
            CancellationToken.None);

        result.LowStockProducts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_MaxIntQuantity_IsNotLowStockAtDefaultThreshold()
    {
        GivenProducts(MakeProduct("Bulk", int.MaxValue));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.LowStockProducts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LargeCatalogue_CountsEveryProduct()
    {
        var products = Enumerable.Range(0, 10_000).Select(i => MakeProduct($"P{i}", i)).ToArray();
        GivenProducts(products);

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.TotalProducts.Should().Be(10_000);
        result.LowStockProducts.Should().HaveCount(10); // quantities 0..9
    }

    [Fact]
    public async Task Handle_MaxLengthProductName_IsCarriedIntoLowStockDto()
    {
        var longName = new string('N', 5000);
        GivenProducts(MakeProduct(longName, 1));

        var result = await _handler.Handle(new GetInventoryDashboardQuery(), CancellationToken.None);

        result.LowStockProducts.Single().Name.Should().HaveLength(5000);
    }
}
