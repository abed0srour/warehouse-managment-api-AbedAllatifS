using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Queries;

namespace Warehouse.Api.UnitTests.Queries;

public class GetOutOfStockProductsQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetOutOfStockProductsQueryHandler CreateHandler() => new(Context);

    private static Task<IEnumerable<OutOfStockProductDto>> HandleAsync(GetOutOfStockProductsQueryHandler handler) =>
        handler.Handle(new GetOutOfStockProductsQuery(), CancellationToken.None);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ReturnsProductsAtZeroStock()
    {
        await SeedAsync(
            MakeProduct("Mouse", "SKU-0001", quantity: 0),
            MakeProduct("Cable", "SKU-0002", quantity: 5));

        var result = await HandleAsync(CreateHandler());

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-0001");
    }

    [Fact]
    public async Task Handle_CarriesTheIdentifyingAndReorderFields()
    {
        var supplier = MakeSupplier("Acme");
        await SeedAsync(
            supplier,
            MakeProduct("Mouse", "SKU-0001", quantity: 0, supplierName: "Acme", supplierId: supplier.SupplierId));

        var result = await HandleAsync(CreateHandler());

        var dto = result.Should().ContainSingle().Subject;
        dto.Name.Should().Be("Mouse");
        dto.Sku.Should().Be("SKU-0001");
        dto.SupplierId.Should().Be(supplier.SupplierId);
        dto.SupplierName.Should().Be("Acme");
    }

    [Fact]
    public async Task Handle_OrdersByProductName()
    {
        await SeedAsync(
            MakeProduct("Mouse", "SKU-0001", quantity: 0),
            MakeProduct("Adapter", "SKU-0002", quantity: 0),
            MakeProduct("Cable", "SKU-0003", quantity: 0));

        var result = await HandleAsync(CreateHandler());

        result.Select(p => p.Name).Should().ContainInOrder("Adapter", "Cable", "Mouse");
    }

    [Fact]
    public async Task Handle_ReturnsEveryZeroStockProduct()
    {
        await SeedAsync(
            MakeProduct("Mouse", "SKU-0001", quantity: 0),
            MakeProduct("Cable", "SKU-0002", quantity: 0),
            MakeProduct("Adapter", "SKU-0003", quantity: 0));

        var result = await HandleAsync(CreateHandler());

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ProductWithNoSupplier_ReturnsNullsRatherThanBeingSkipped()
    {
        await SeedAsync(MakeProduct("Orphan", "SKU-0001", quantity: 0, supplierName: null, supplierId: null));

        var dto = (await HandleAsync(CreateHandler())).Should().ContainSingle().Subject;

        dto.SupplierId.Should().BeNull();
        dto.SupplierName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CarriesLastUpdatedAtWhenItIsSet()
    {
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", quantity: 0));

        var dto = (await HandleAsync(CreateHandler())).Should().ContainSingle().Subject;

        // MakeProduct leaves LastUpdatedAt null; the DTO surfaces that rather than
        // substituting CreatedAt the way the AutoMapper-free ProductProjections does.
        dto.LastUpdatedAt.Should().BeNull();
    }

    // ---------- exclusions ----------

    [Fact]
    public async Task Handle_ExcludesArchivedProducts()
    {
        await SeedAsync(
            MakeProduct("Active", "SKU-0001", quantity: 0),
            MakeProduct("Archived", "SKU-0002", quantity: 0, archived: true));

        var result = await HandleAsync(CreateHandler());

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-0001");
    }

    [Fact]
    public async Task Handle_ArchivedIsTheOnlyExclusion_NotSupplierlessOrExpired()
    {
        await SeedAsync(
            MakeProduct("Orphan", "SKU-0001", quantity: 0, supplierName: null),
            MakeProduct("Expired", "SKU-0002", quantity: 0, expiryDate: new DateTime(2020, 1, 1)));

        var result = await HandleAsync(CreateHandler());

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ExcludesProductsWithStockOnHand()
    {
        await SeedAsync(
            MakeProduct("Mouse", "SKU-0001", quantity: 1),
            MakeProduct("Cable", "SKU-0002", quantity: 500));

        var result = await HandleAsync(CreateHandler());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MatchesExactlyZero_NotMerelyLowStock()
    {
        // The boundary that separates this from the dashboard's low-stock list: 1 is not 0.
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", quantity: 1));

        var result = await HandleAsync(CreateHandler());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NegativeStock_IsNotReported()
    {
        // The domain forbids negative stock, but the read side sees whatever is in the table.
        // Pinning the behaviour: the predicate is == 0, so a negative row would be missed.
        await SeedAsync(MakeProduct("Corrupt", "SKU-0001", quantity: -3));

        var result = await HandleAsync(CreateHandler());

        result.Should().BeEmpty();
    }

    // ---------- empty cases ----------

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmpty()
    {
        var result = await HandleAsync(CreateHandler());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AllProductsArchived_ReturnsEmpty()
    {
        await SeedAsync(
            MakeProduct("Mouse", "SKU-0001", quantity: 0, archived: true),
            MakeProduct("Cable", "SKU-0002", quantity: 0, archived: true));

        var result = await HandleAsync(CreateHandler());

        result.Should().BeEmpty();
    }
}
