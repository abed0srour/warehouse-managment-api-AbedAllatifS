using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Queries;

namespace Warehouse.Api.UnitTests.Queries;

public class GetProductsBySupplierQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetProductsBySupplierQueryHandler CreateHandler() => new(Context, Mapper);

    [Fact]
    public async Task Handle_MatchesOnDenormalisedSupplierName()
    {
        await SeedAsync(
            MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"),
            MakeProduct("Cable", "SKU-0002", supplierName: "Globex"));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme"), CancellationToken.None);

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-0001");
    }

    [Fact]
    public async Task Handle_MatchesOnRelatedSupplierName()
    {
        var supplier = MakeSupplier("Acme");
        await SeedAsync(supplier, MakeProduct("Mouse", "SKU-0001", supplierName: null, supplierId: supplier.SupplierId));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme"), CancellationToken.None);

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-0001");
    }

    [Fact]
    public async Task Handle_DefaultSortOrder_IsNewestFirst()
    {
        await SeedAsync(
            MakeProduct("Old", "SKU-0001", supplierName: "Acme", createdAt: new DateTime(2024, 1, 1)),
            MakeProduct("New", "SKU-0002", supplierName: "Acme", createdAt: new DateTime(2026, 1, 1)));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme"), CancellationToken.None);

        result.Select(p => p.Name).Should().ContainInOrder("New", "Old");
    }

    [Fact]
    public async Task Handle_AscendingSortOrder_IsOldestFirst()
    {
        await SeedAsync(
            MakeProduct("Old", "SKU-0001", supplierName: "Acme", createdAt: new DateTime(2024, 1, 1)),
            MakeProduct("New", "SKU-0002", supplierName: "Acme", createdAt: new DateTime(2026, 1, 1)));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme", "asc"), CancellationToken.None);

        result.Select(p => p.Name).Should().ContainInOrder("Old", "New");
    }

    [Fact]
    public async Task Handle_SortOrderIsCaseInsensitive()
    {
        await SeedAsync(
            MakeProduct("Old", "SKU-0001", supplierName: "Acme", createdAt: new DateTime(2024, 1, 1)),
            MakeProduct("New", "SKU-0002", supplierName: "Acme", createdAt: new DateTime(2026, 1, 1)));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme", "ASC"), CancellationToken.None);

        result.Select(p => p.Name).Should().ContainInOrder("Old", "New");
    }

    [Fact]
    public async Task Handle_UnrecognisedSortOrder_FallsBackToDescending()
    {
        await SeedAsync(
            MakeProduct("Old", "SKU-0001", supplierName: "Acme", createdAt: new DateTime(2024, 1, 1)),
            MakeProduct("New", "SKU-0002", supplierName: "Acme", createdAt: new DateTime(2026, 1, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsBySupplierQuery("Acme", "sideways"),
            CancellationToken.None);

        result.Select(p => p.Name).Should().ContainInOrder("New", "Old");
    }

    [Fact]
    public async Task Handle_IncludesArchivedProducts()
    {
        await SeedAsync(
            MakeProduct("Active", "SKU-0001", supplierName: "Acme"),
            MakeProduct("Archived", "SKU-0002", supplierName: "Acme", archived: true));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme"), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_UnknownSupplier_ReturnsEmpty()
    {
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Nonexistent"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmpty()
    {
        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NullSortOrder_ThrowsNullReference()
    {
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"));

        var act = async () => await CreateHandler().Handle(
            new GetProductsBySupplierQuery("Acme", null!),
            CancellationToken.None);

        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task Handle_NullSupplierName_ReturnsProductsWithNullSupplier()
    {
        await SeedAsync(
            MakeProduct("Orphan", "SKU-0001", supplierName: null),
            MakeProduct("Owned", "SKU-0002", supplierName: "Acme"));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery(null!), CancellationToken.None);

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-0001");
    }

    [Fact]
    public async Task Handle_EmptySupplierName_ReturnsEmpty()
    {
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery(string.Empty), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SupplierNameMatchIsCaseSensitive()
    {
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("acme"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MaxLengthSupplierName_MatchesExactly()
    {
        var longName = new string('S', 4000);
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: longName));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery(longName), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_SupplierNameDifferingOnlyInTrailingWhitespace_DoesNotMatch()
    {
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme "), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnicodeSupplierName_MatchesExactly()
    {
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Zأ¼rich Imports"));

        var result = await CreateHandler().Handle(
            new GetProductsBySupplierQuery("Zأ¼rich Imports"),
            CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ProductsWithIdenticalCreatedAt_AreAllReturned()
    {
        var timestamp = new DateTime(2026, 1, 1);
        await SeedAsync(
            MakeProduct("A", "SKU-0001", supplierName: "Acme", createdAt: timestamp),
            MakeProduct("B", "SKU-0002", supplierName: "Acme", createdAt: timestamp));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("Acme"), CancellationToken.None);

        result.Should().HaveCount(2);
    }
}
