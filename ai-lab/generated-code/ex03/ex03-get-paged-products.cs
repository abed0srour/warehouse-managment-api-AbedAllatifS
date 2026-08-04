using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Queries;

namespace Warehouse.Api.UnitTests.Queries;

public class GetPagedProductsQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetPagedProductsQueryHandler CreateHandler() => new(Context, Mapper);

    private async Task SeedProductsAsync(int count)
    {
        var products = Enumerable.Range(1, count)
            .Select(i => MakeProduct($"Product {i:D4}", $"SKU-{i:D4}"))
            .Cast<object>()
            .ToArray();
        await SeedAsync(products);
    }

    // ---------- positive ----------

    [Fact]
    public async Task Handle_FirstPage_ReturnsRequestedPageSize()
    {
        await SeedProductsAsync(25);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, 10), CancellationToken.None);

        result.Items.Should().HaveCount(10);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task Handle_LastPartialPage_ReturnsRemainderOnly()
    {
        await SeedProductsAsync(25);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(3, 10), CancellationToken.None);

        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task Handle_ConsecutivePages_DoNotOverlap()
    {
        await SeedProductsAsync(30);
        var handler = CreateHandler();

        var page1 = await handler.Handle(new GetPagedProductsQuery(1, 10), CancellationToken.None);
        var page2 = await handler.Handle(new GetPagedProductsQuery(2, 10), CancellationToken.None);

        page1.Items.Select(p => p.Id).Should().NotIntersectWith(page2.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Handle_TotalCountIgnoresPaging()
    {
        await SeedProductsAsync(42);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(2, 5), CancellationToken.None);

        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(42);
    }

    [Fact]
    public async Task Handle_DefaultParameters_ReturnFirstTenItems()
    {
        await SeedProductsAsync(15);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(10);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ArchivedProducts_AreIncluded()
    {
        await SeedAsync(
            MakeProduct("Active", "SKU-0001"),
            MakeProduct("Archived", "SKU-0002", archived: true));

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, 10), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(p => p.IsArchived);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyPageWithZeroTotal()
    {
        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, 10), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PageBeyondEnd_ReturnsEmptyItemsButRealTotal()
    {
        await SeedProductsAsync(5);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(99, 10), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_PageSizeZero_ReturnsNoItems()
    {
        // DEFECT: PageSize is not validated. Zero silently yields an empty page instead of
        // a 400. The caller cannot distinguish this from "no data".
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, 0), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_PageNumberZero_SilentlyBehavesAsFirstPage()
    {
        // DEFECT: PageNumber is not validated. (0 - 1) * 10 = -10, and Skip(-10) is treated
        // as Skip(0) by LINQ, so page 0 quietly returns page 1 rather than being rejected.
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(0, 5), CancellationToken.None);

        result.Items.Should().HaveCount(5);
        result.PageNumber.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NegativePageNumber_SilentlyBehavesAsFirstPage()
    {
        // DEFECT: same root cause -- a negative Skip is clamped to zero rather than rejected.
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(-5, 5), CancellationToken.None);

        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Handle_NegativePageSize_SilentlyReturnsNoItems()
    {
        // DEFECT: a negative Take is clamped to zero by LINQ rather than rejected, so a
        // nonsense page size returns an empty page that is indistinguishable from "no data"
        // -- while TotalCount still reports rows exist.
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, -5), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(10);
    }

    // ---------- edge cases: integer overflow ----------

    [Fact]
    public async Task Handle_MaxIntPageNumber_OverflowsSkipAndReturnsFirstPage()
    {
        // DEFECT (integer overflow): (int.MaxValue - 1) * 10 does not fit in an Int32.
        // C# arithmetic is unchecked by default, so it wraps to -20, Skip(-20) clamps to 0,
        // and the "last page in the universe" silently returns the FIRST page of data.
        // A caller paging with a corrupted page number gets plausible-looking wrong results.
        await SeedProductsAsync(20);

        var result = await CreateHandler().Handle(
            new GetPagedProductsQuery(int.MaxValue, 10),
            CancellationToken.None);

        unchecked((int.MaxValue - 1) * 10).Should().Be(-20, "the overflow is what drives this behaviour");
        result.Items.Should().HaveCount(10, "the overflow wraps to a negative Skip, which LINQ clamps to zero");
    }

    [Fact]
    public async Task Handle_MaxIntPageSize_DoesNotOverflowOnFirstPage()
    {
        // Page 1 is safe regardless of PageSize, because (1 - 1) * PageSize is always 0.
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(
            new GetPagedProductsQuery(1, int.MaxValue),
            CancellationToken.None);

        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_LargePageNumberAndSizeCombination_Overflows()
    {
        // DEFECT: 100_000 * 100_000 = 10^10, far beyond Int32. Wraps to 1_410_065_408,
        // a positive value, so this one skips a nonsense number of rows instead of clamping.
        await SeedProductsAsync(5);

        var result = await CreateHandler().Handle(
            new GetPagedProductsQuery(100_001, 100_000),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(5);
    }

    // ---------- edge cases: data ----------

    [Fact]
    public async Task Handle_MaxLengthProductName_SurvivesProjection()
    {
        await SeedAsync(MakeProduct(new string('N', 8000), "SKU-0001"));

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, 10), CancellationToken.None);

        result.Items.Single().Name.Should().HaveLength(8000);
    }

    [Fact]
    public async Task Handle_OrderingIsStableAcrossCalls()
    {
        await SeedProductsAsync(20);
        var handler = CreateHandler();

        var first = await handler.Handle(new GetPagedProductsQuery(1, 20), CancellationToken.None);
        var second = await handler.Handle(new GetPagedProductsQuery(1, 20), CancellationToken.None);

        first.Items.Select(p => p.Id).Should().ContainInOrder(second.Items.Select(p => p.Id));
    }
}
