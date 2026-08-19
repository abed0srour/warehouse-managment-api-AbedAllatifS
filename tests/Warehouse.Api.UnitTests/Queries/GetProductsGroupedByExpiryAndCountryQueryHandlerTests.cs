using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Queries;

namespace Warehouse.Api.UnitTests.Queries;

public class GetProductsGroupedByExpiryAndCountryQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetProductsGroupedByExpiryAndCountryQueryHandler CreateHandler() => new(Context);

    private static DateTime Unspecified(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, second), DateTimeKind.Unspecified);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_GroupsByYearAndCountry()
    {
        var us = MakeSupplier("Acme", "US");
        var de = MakeSupplier("Globex", "DE");
        await SeedAsync(us, de,
            MakeProduct("A", "SKU-0001", supplierId: us.SupplierId, expiryDate: Unspecified(2025, 3, 1)),
            MakeProduct("B", "SKU-0002", supplierId: de.SupplierId, expiryDate: Unspecified(2025, 3, 1)),
            MakeProduct("C", "SKU-0003", supplierId: us.SupplierId, expiryDate: Unspecified(2026, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result.Single(g => g.Year == 2025 && g.Country == "US").TotalProducts.Should().Be(1);
        result.Single(g => g.Year == 2025 && g.Country == "DE").TotalProducts.Should().Be(1);
        result.Single(g => g.Year == 2026 && g.Country == "US").TotalProducts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SameYearAndCountry_CollapseIntoOneGroup()
    {
        var us = MakeSupplier("Acme", "US");
        await SeedAsync(us,
            MakeProduct("A", "SKU-0001", supplierId: us.SupplierId, expiryDate: Unspecified(2025, 3, 1)),
            MakeProduct("B", "SKU-0002", supplierId: us.SupplierId, expiryDate: Unspecified(2025, 11, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().TotalProducts.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DifferentSuppliersSameCountry_ShareAGroup()
    {
        var acme = MakeSupplier("Acme", "US");
        var initech = MakeSupplier("Initech", "US");
        await SeedAsync(acme, initech,
            MakeProduct("A", "SKU-0001", supplierId: acme.SupplierId, expiryDate: Unspecified(2025, 3, 1)),
            MakeProduct("B", "SKU-0002", supplierId: initech.SupplierId, expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().ContainSingle().Which.TotalProducts.Should().Be(2);
    }

    [Fact]
    public async Task Handle_GroupCarriesProjectedProducts()
    {
        var us = MakeSupplier("Acme", "US");
        await SeedAsync(us,
            MakeProduct("Milk", "SKU-0001", quantity: 7, supplierId: us.SupplierId, expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        var vm = result.Single().Products.Single();
        vm.Name.Should().Be("Milk");
        vm.QuantityInStock.Should().Be(7);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsNoGroups()
    {
        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProductWithoutSupplier_IsExcluded()
    {
        // Requires BOTH an expiry date and a supplier. An unassigned product silently
        // vanishes from this report even though it has an expiry date.
        await SeedAsync(MakeProduct("Orphan", "SKU-0001", supplierId: null, expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProductWithoutExpiryDate_IsExcluded()
    {
        var us = MakeSupplier("Acme", "US");
        await SeedAsync(us, MakeProduct("NoExpiry", "SKU-0001", supplierId: us.SupplierId, expiryDate: null));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DanglingSupplierId_IsExcluded()
    {
        // A SupplierId pointing at a row that does not exist leaves the navigation null,
        // so the product drops out rather than surfacing as an error.
        await SeedAsync(MakeProduct("Dangling", "SKU-0001",
            supplierId: Guid.NewGuid(), expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DisposedContext_ThrowsObjectDisposed()
    {
        var handler = CreateHandler();
        Context.Dispose();

        var act = async () => await handler.Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ---------- edge cases ----------

    [Fact]
    public async Task Handle_YearBoundary_SplitsGroupsWithinSameCountry()
    {
        var us = MakeSupplier("Acme", "US");
        await SeedAsync(us,
            MakeProduct("Before", "SKU-0001", supplierId: us.SupplierId,
                expiryDate: Unspecified(2025, 12, 31, 23, 59, 59)),
            MakeProduct("After", "SKU-0002", supplierId: us.SupplierId,
                expiryDate: Unspecified(2026, 1, 1, 0, 0, 0)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(g => g.Year).Should().BeEquivalentTo(new[] { 2025, 2026 });
        result.Should().OnlyContain(g => g.Country == "US");
    }

    [Fact]
    public async Task Handle_CountryComparisonIsCaseSensitive()
    {
        // "US" and "us" produce two separate groups. Nothing normalises supplier country,
        // so inconsistent data fragments the report.
        var upper = MakeSupplier("Acme", "US");
        var lower = MakeSupplier("Globex", "us");
        await SeedAsync(upper, lower,
            MakeProduct("A", "SKU-0001", supplierId: upper.SupplierId, expiryDate: Unspecified(2025, 3, 1)),
            MakeProduct("B", "SKU-0002", supplierId: lower.SupplierId, expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_UnicodeCountryName_FormsItsOwnGroup()
    {
        var supplier = MakeSupplier("Abidjan Foods", "Côte d'Ivoire");
        await SeedAsync(supplier,
            MakeProduct("Cocoa", "SKU-0001", supplierId: supplier.SupplierId, expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Single().Country.Should().Be("Côte d'Ivoire");
    }

    [Fact]
    public async Task Handle_MaxLengthCountryName_IsPreserved()
    {
        var longCountry = new string('C', 2000);
        var supplier = MakeSupplier("Acme", longCountry);
        await SeedAsync(supplier,
            MakeProduct("A", "SKU-0001", supplierId: supplier.SupplierId, expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Single().Country.Should().HaveLength(2000);
    }

    [Fact]
    public async Task Handle_LeapDayExpiry_GroupsIntoLeapYear()
    {
        var us = MakeSupplier("Acme", "US");
        await SeedAsync(us,
            MakeProduct("LeapDay", "SKU-0001", supplierId: us.SupplierId, expiryDate: Unspecified(2028, 2, 29)));

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Single().Year.Should().Be(2028);
    }

    [Fact]
    public async Task Handle_ManyCountryYearCombinations_ProduceCartesianGroups()
    {
        var suppliers = new[] { MakeSupplier("S1", "US"), MakeSupplier("S2", "DE"), MakeSupplier("S3", "FR") };
        var products = suppliers
            .SelectMany(s => Enumerable.Range(2024, 4)
                .Select(year => (object)MakeProduct($"{s.Country}-{year}", $"SKU-{s.Country}-{year}",
                    supplierId: s.SupplierId, expiryDate: Unspecified(year, 6, 15))))
            .ToArray();
        await SeedAsync(suppliers.Cast<object>().ToArray());
        await SeedAsync(products);

        var result = await CreateHandler().Handle(
            new GetProductsGroupedByExpiryAndCountryQuery(),
            CancellationToken.None);

        result.Should().HaveCount(12); // 3 countries x 4 years
        result.Should().OnlyContain(g => g.TotalProducts == 1);
    }
}
