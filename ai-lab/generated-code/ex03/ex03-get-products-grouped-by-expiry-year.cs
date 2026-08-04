using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Queries;

namespace Warehouse.Api.UnitTests.Queries;

public class GetProductsGroupedByExpiryYearQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetProductsGroupedByExpiryYearQueryHandler CreateHandler() => new(Context);

    private static DateTime Unspecified(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, second), DateTimeKind.Unspecified);

    [Fact]
    public async Task Handle_GroupsProductsByExpiryYear()
    {
        await SeedAsync(
            MakeProduct("A", "SKU-0001", expiryDate: Unspecified(2025, 3, 1)),
            MakeProduct("B", "SKU-0002", expiryDate: Unspecified(2025, 9, 1)),
            MakeProduct("C", "SKU-0003", expiryDate: Unspecified(2026, 1, 1)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Single(g => g.Year == 2025).TotalProducts.Should().Be(2);
        result.Single(g => g.Year == 2026).TotalProducts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_GroupCarriesItsProducts()
    {
        await SeedAsync(MakeProduct("Milk", "SKU-0001", expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        var group = result.Single();
        group.Products.Should().ContainSingle().Which.Name.Should().Be("Milk");
    }

    [Fact]
    public async Task Handle_TotalProductsMatchesProductsCount()
    {
        await SeedAsync(
            MakeProduct("A", "SKU-0001", expiryDate: Unspecified(2025, 3, 1)),
            MakeProduct("B", "SKU-0002", expiryDate: Unspecified(2025, 4, 1)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        var group = result.Single();
        group.TotalProducts.Should().Be(group.Products.Count());
    }

    [Fact]
    public async Task Handle_ProjectsProductFieldsIntoViewModel()
    {
        await SeedAsync(MakeProduct("Milk", "SKU-0001", quantity: 7, price: 4.5m,
            supplierName: "Acme", expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        var vm = result.Single().Products.Single();
        vm.Sku.Should().Be("SKU-0001");
        vm.QuantityInStock.Should().Be(7);
        vm.Price.Should().Be(4.5m);
        vm.SupplierName.Should().Be("Acme");
    }

    [Fact]
    public async Task Handle_NullLastUpdatedAt_FallsBackToCreatedAt()
    {
        var createdAt = new DateTime(2024, 6, 1);
        await SeedAsync(MakeProduct("Milk", "SKU-0001", expiryDate: Unspecified(2025, 3, 1), createdAt: createdAt));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Single().Products.Single().LastUpdatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task Handle_ArchivedProductsAreStillGrouped()
    {
        await SeedAsync(MakeProduct("Retired", "SKU-0001", expiryDate: Unspecified(2025, 3, 1), archived: true));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Single().TotalProducts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsNoGroups()
    {
        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProductsWithoutExpiryDate_AreExcluded()
    {
        await SeedAsync(
            MakeProduct("NoExpiry", "SKU-0001", expiryDate: null),
            MakeProduct("HasExpiry", "SKU-0002", expiryDate: Unspecified(2025, 3, 1)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Products.Should().ContainSingle().Which.Sku.Should().Be("SKU-0002");
    }

    [Fact]
    public async Task Handle_AllProductsLackExpiryDate_ReturnsNoGroups()
    {
        await SeedAsync(
            MakeProduct("A", "SKU-0001", expiryDate: null),
            MakeProduct("B", "SKU-0002", expiryDate: null));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DisposedContext_ThrowsObjectDisposed()
    {
        var handler = CreateHandler();
        Context.Dispose();

        var act = async () => await handler.Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Handle_LastInstantOfYear_GroupsIntoThatYear()
    {
        await SeedAsync(MakeProduct("NewYearsEve", "SKU-0001", expiryDate: Unspecified(2025, 12, 31, 23, 59, 59)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Single().Year.Should().Be(2025);
    }

    [Fact]
    public async Task Handle_FirstInstantOfYear_GroupsIntoThatYear()
    {
        await SeedAsync(MakeProduct("NewYearsDay", "SKU-0001", expiryDate: Unspecified(2026, 1, 1, 0, 0, 0)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Single().Year.Should().Be(2026);
    }

    [Fact]
    public async Task Handle_OneSecondApartAcrossMidnight_LandsInDifferentGroups()
    {
        await SeedAsync(
            MakeProduct("Before", "SKU-0001", expiryDate: Unspecified(2025, 12, 31, 23, 59, 59)),
            MakeProduct("After", "SKU-0002", expiryDate: Unspecified(2026, 1, 1, 0, 0, 0)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Single(g => g.Year == 2025).Products.Single().Sku.Should().Be("SKU-0001");
        result.Single(g => g.Year == 2026).Products.Single().Sku.Should().Be("SKU-0002");
    }

    [Fact]
    public async Task Handle_GroupingUsesStoredValueWithNoTimezoneConversion()
    {
        var lateOnNewYearsEve = Unspecified(2025, 12, 31, 23, 30, 0);
        await SeedAsync(MakeProduct("Edge", "SKU-0001", expiryDate: lateOnNewYearsEve));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Single().Year.Should().Be(2025);
        result.Single().Products.Single().ExpiryDate.Should().Be(lateOnNewYearsEve);
    }

    [Fact]
    public async Task Handle_UtcKindExpiryDate_IsGroupedByItsRawYearNotConverted()
    {
        await SeedAsync(MakeProduct("Edge", "SKU-0001",
            expiryDate: DateTime.SpecifyKind(new DateTime(2025, 12, 31, 23, 30, 0), DateTimeKind.Utc)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Single().Year.Should().Be(2025);
    }

    [Fact]
    public async Task Handle_LeapDayExpiry_GroupsIntoLeapYear()
    {
        await SeedAsync(MakeProduct("LeapDay", "SKU-0001", expiryDate: Unspecified(2028, 2, 29)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Single().Year.Should().Be(2028);
    }

    [Fact]
    public async Task Handle_ExtremeDateTimeValues_ProduceTheirOwnGroups()
    {
        await SeedAsync(
            MakeProduct("MinDate", "SKU-0001", expiryDate: DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Unspecified)),
            MakeProduct("MaxDate", "SKU-0002", expiryDate: DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Unspecified)));

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Select(g => g.Year).Should().BeEquivalentTo(new[] { 1, 9999 });
    }

    [Fact]
    public async Task Handle_ManyDistinctYears_ProducesOneGroupPerYear()
    {
        var products = Enumerable.Range(0, 50)
            .Select(i => (object)MakeProduct($"P{i}", $"SKU-{i:D5}", expiryDate: Unspecified(2000 + i, 6, 15)))
            .ToArray();
        await SeedAsync(products);

        var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

        result.Should().HaveCount(50);
        result.Should().OnlyContain(g => g.TotalProducts == 1);
    }
}
