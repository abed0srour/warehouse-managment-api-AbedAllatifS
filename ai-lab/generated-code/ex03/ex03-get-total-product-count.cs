using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Queries;

namespace Warehouse.Api.UnitTests.Queries;

public class GetTotalProductCountQueryHandlerTests : EfQueryHandlerTestBase
{
    private GetTotalProductCountQueryHandler CreateHandler() => new(Context);

    [Fact]
    public async Task Handle_ReturnsNumberOfProducts()
    {
        await SeedAsync(
            MakeProduct("A", "SKU-0001"),
            MakeProduct("B", "SKU-0002"),
            MakeProduct("C", "SKU-0003"));

        var result = await CreateHandler().Handle(new GetTotalProductCountQuery(), CancellationToken.None);

        result.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CountsArchivedProductsToo()
    {
        await SeedAsync(
            MakeProduct("Active", "SKU-0001"),
            MakeProduct("Archived", "SKU-0002", archived: true));

        var result = await CreateHandler().Handle(new GetTotalProductCountQuery(), CancellationToken.None);

        result.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsOutOfStockProducts()
    {
        await SeedAsync(
            MakeProduct("InStock", "SKU-0001", quantity: 10),
            MakeProduct("OutOfStock", "SKU-0002", quantity: 0));

        var result = await CreateHandler().Handle(new GetTotalProductCountQuery(), CancellationToken.None);

        result.Should().Be(2);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZero()
    {
        var result = await CreateHandler().Handle(new GetTotalProductCountQuery(), CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CancelledToken_ThrowsOperationCanceled()
    {
        await SeedAsync(MakeProduct("A", "SKU-0001"));

        var act = async () => await CreateHandler().Handle(
            new GetTotalProductCountQuery(),
            new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_DisposedContext_ThrowsObjectDisposed()
    {
        var handler = CreateHandler();
        Context.Dispose();

        var act = async () => await handler.Handle(new GetTotalProductCountQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Handle_LargeCatalogue_ReturnsExactCount()
    {
        var products = Enumerable.Range(1, 5_000)
            .Select(i => (object)MakeProduct($"P{i}", $"SKU-{i:D5}"))
            .ToArray();
        await SeedAsync(products);

        var result = await CreateHandler().Handle(new GetTotalProductCountQuery(), CancellationToken.None);

        result.Should().Be(5_000);
    }

    [Fact]
    public async Task Handle_ReturnTypeIsInt32_WhichCapsAtMaxValue()
    {
        await SeedAsync(MakeProduct("A", "SKU-0001"));

        var result = await CreateHandler().Handle(new GetTotalProductCountQuery(), CancellationToken.None);

        result.Should().BeOfType(typeof(int));
        result.Should().BeLessThanOrEqualTo(int.MaxValue);
    }
}
