using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Queries;

namespace Warehouse.Api.UnitTests.Queries;

public class GetProductsBySupplierQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetProductsBySupplierQueryHandler CreateHandler() => new(Context, Mapper);

    // ---------- positive ----------

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
        // The predicate also matches through the navigation property, so a product whose
        // denormalised SupplierName is stale still resolves via its FK.
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

    // ---------- negative ----------

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
        // DEFECT: SortOrder has a default of "desc" but is never null-checked. A caller that
        // binds ?sortOrder= to an explicit null (or any client that passes null) gets a
        // NullReferenceException from request.SortOrder.ToLower() -- a 500, not a 400.
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"));

        var act = async () => await CreateHandler().Handle(
            new GetProductsBySupplierQuery("Acme", null!),
            CancellationToken.None);

        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task Handle_NullSupplierName_ReturnsProductsWithNullSupplier()
    {
        // DEFECT: a null SupplierName is not rejected; it matches every product whose
        // denormalised SupplierName is also null, which is a surprising result for a
        // "find by supplier" query.
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
        // Worth pinning: the name comparison is exact, unlike the sort-order comparison
        // right below it, which is lowercased. "acme" does not find "Acme".
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Acme"));

        var result = await CreateHandler().Handle(new GetProductsBySupplierQuery("acme"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ---------- edge cases ----------

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
        await SeedAsync(MakeProduct("Mouse", "SKU-0001", supplierName: "Zürich Imports"));

        var result = await CreateHandler().Handle(
            new GetProductsBySupplierQuery("Zürich Imports"),
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
