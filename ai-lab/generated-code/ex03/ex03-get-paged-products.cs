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
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, 0), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_PageNumberZero_SilentlyBehavesAsFirstPage()
    {
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(0, 5), CancellationToken.None);

        result.Items.Should().HaveCount(5);
        result.PageNumber.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NegativePageNumber_SilentlyBehavesAsFirstPage()
    {
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(-5, 5), CancellationToken.None);

        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Handle_NegativePageSize_SilentlyReturnsNoItems()
    {
        await SeedProductsAsync(10);

        var result = await CreateHandler().Handle(new GetPagedProductsQuery(1, -5), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_MaxIntPageNumber_OverflowsSkipAndReturnsFirstPage()
    {
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
        await SeedProductsAsync(5);

        var result = await CreateHandler().Handle(
            new GetPagedProductsQuery(100_001, 100_000),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(5);
    }

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
