using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Warehouse.Application;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Queries;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class SearchProductsQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly SearchProductsQueryHandler _handler;

    public SearchProductsQueryHandlerTests()
    {
        _mapper.Setup(m => m.Map<ProductViewModel>(It.IsAny<Product>()))
            .Returns((Product p) => ToViewModel(p));

        _handler = new SearchProductsQueryHandler(_productRepository.Object, _mapper.Object);
    }

    private static ProductViewModel ToViewModel(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Sku = p.Sku,
        Description = p.Description,
        Price = p.Price,
        QuantityInStock = p.QuantityInStock,
        SupplierName = p.SupplierName!,
        ExpiryDate = p.ExpiryDate,
        IsArchived = p.IsArchived,
        CreatedAt = p.CreatedAt,
        LastUpdatedAt = p.LastUpdatedAt ?? default
    };

    private static Product MakeProduct(string name, string sku, string? supplierName)
    {
        var product = Product.Create(name, sku, 10m, 5);
        product.SupplierName = supplierName;
        return product;
    }

    private static Product Reconstruct(
        string name,
        string sku,
        string? supplierName = null,
        int quantity = 5,
        bool archived = false,
        DateTime? expiryDate = null,
        DateTime? createdAt = null,
        DateTime? lastUpdatedAt = null) =>
        Product.Reconstruct(
            Guid.NewGuid(),
            name,
            sku,
            string.Empty,
            10m,
            quantity,
            supplierName,
            null,
            expiryDate,
            archived,
            createdAt ?? new DateTime(2026, 1, 1),
            lastUpdatedAt);

    private void GivenProducts(params Product[] products) =>
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

    private SearchProductsQueryHandler HandlerWithRealMapper()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance);

        return new SearchProductsQueryHandler(_productRepository.Object, configuration.CreateMapper());
    }

    [Fact]
    public async Task Handle_SearchByName_ReturnsMatches()
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("Wireless Keyboard", "SKU-002", "Acme"),
            MakeProduct("USB Cable", "SKU-003", "Globex"));

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(p => p.Sku).Should().BeEquivalentTo("SKU-001", "SKU-002");
    }

    [Fact]
    public async Task Handle_SearchBySupplier_ReturnsMatches()
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("Wireless Keyboard", "SKU-002", "Acme"),
            MakeProduct("USB Cable", "SKU-003", "Globex"));

        var result = await _handler.Handle(new SearchProductsQuery(null, "Globex"), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Sku.Should().Be("SKU-003");
    }

    [Fact]
    public async Task Handle_SearchByBothFilters_ReturnsIntersection()
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("Wireless Keyboard", "SKU-002", "Globex"),
            MakeProduct("USB Cable", "SKU-003", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", "Acme"), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Sku.Should().Be("SKU-001");
    }

    [Fact]
    public async Task Handle_EmptyFilters_ReturnsAllProducts()
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("USB Cable", "SKU-002", "Globex"));

        var result = await _handler.Handle(new SearchProductsQuery(null, null), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("wireless")]
    [InlineData("WIRELESS")]
    [InlineData("WiReLeSs")]
    public async Task Handle_NameFilter_IgnoresCase(string term)
    {
        GivenProducts(MakeProduct("Wireless Mouse", "SKU-001", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery(term, null), CancellationToken.None);

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-001");
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("ACME")]
    [InlineData("aCmE")]
    public async Task Handle_SupplierFilter_IgnoresCase(string term)
    {
        GivenProducts(MakeProduct("Wireless Mouse", "SKU-001", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery(null, term), CancellationToken.None);

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-001");
    }

    [Fact]
    public async Task Handle_NameFilter_MatchesSubstringAnywhereNotJustPrefix()
    {
        GivenProducts(MakeProduct("Heavy Duty Pallet Jack", "SKU-001", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery("Pallet", null), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Handle_BlankFilter_IsTreatedAsNoFilterRatherThanNoMatch(string blank)
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("USB Cable", "SKU-002", "Globex"));

        var result = await _handler.Handle(new SearchProductsQuery(blank, blank), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_AnySearch_QueriesRepositoryExactlyOnce()
    {
        GivenProducts(MakeProduct("Wireless Mouse", "SKU-001", "Acme"));

        await _handler.Handle(new SearchProductsQuery("Wireless", "Acme"), CancellationToken.None);

        _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Always_ForwardsCallerTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        GivenProducts(MakeProduct("Wireless Mouse", "SKU-001", "Acme"));

        await _handler.Handle(new SearchProductsQuery(null, null), cts.Token);

        _productRepository.Verify(r => r.GetAllAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task Handle_MapsOnlyTheMatchingProducts_NotEveryRowFetched()
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("USB Cable", "SKU-002", "Globex"),
            MakeProduct("HDMI Cable", "SKU-003", "Globex"));

        await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        _mapper.Verify(m => m.Map<ProductViewModel>(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsMaterialisedResult_NotADeferredQuery()
    {
        GivenProducts(MakeProduct("Wireless Mouse", "SKU-001", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        _ = result.ToList();
        _ = result.ToList();

        _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mapper.Verify(m => m.Map<ProductViewModel>(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyCollection()
    {
        GivenProducts();

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NothingMatchesFilter_ReturnsEmptyCollectionRatherThanThrowing()
    {
        GivenProducts(MakeProduct("USB Cable", "SKU-001", "Globex"));

        var result = await _handler.Handle(new SearchProductsQuery("Forklift", null), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var act = async () => await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
    }

    [Fact]
    public async Task Handle_MapperThrows_PropagatesException()
    {
        GivenProducts(MakeProduct("Wireless Mouse", "SKU-001", "Acme"));
        _mapper.Setup(m => m.Map<ProductViewModel>(It.IsAny<Product>()))
            .Throws(new AutoMapperMappingException("unmapped member"));

        var act = async () => await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        await act.Should().ThrowAsync<AutoMapperMappingException>();
    }

    [Fact]
    public async Task Handle_RepositoryReturnsNull_ThrowsArgumentNullException()
    {
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Product>)null!);

        var act = async () => await _handler.Handle(new SearchProductsQuery(null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ProductWithNullName_ThrowsNullReferenceException()
    {
        GivenProducts(Reconstruct(null!, "SKU-001"));

        var act = async () => await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task Handle_ProductWithNullSupplierName_IsExcludedWithoutThrowing()
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", null),
            MakeProduct("Wireless Keyboard", "SKU-002", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery(null, "Acme"), CancellationToken.None);

        result.Should().ContainSingle().Which.Sku.Should().Be("SKU-002");
    }

    [Fact]
    public async Task Handle_MaxLengthName_MatchesEmbeddedSubstringIntact()
    {
        var longName = new string('X', 4000) + "Forklift" + new string('Y', 4000);
        GivenProducts(Reconstruct(longName, "SKU-001"));

        var result = await _handler.Handle(new SearchProductsQuery("Forklift", null), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().HaveLength(8008);
    }

    [Fact]
    public async Task Handle_SearchTermLongerThanProductName_ReturnsNoMatchWithoutThrowing()
    {
        GivenProducts(MakeProduct("Nut", "SKU-001", "Acme"));

        var result = await _handler.Handle(
            new SearchProductsQuery(new string('N', 10_000), null),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MaxIntQuantity_SurvivesProjectionWithoutOverflow()
    {
        GivenProducts(Reconstruct("Bulk Pallet", "SKU-001", quantity: int.MaxValue));

        var result = await _handler.Handle(new SearchProductsQuery("Bulk", null), CancellationToken.None);

        result.Single().QuantityInStock.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task Handle_ArchivedProducts_AreIncludedInSearchResults()
    {
        GivenProducts(
            Reconstruct("Wireless Mouse", "SKU-001", archived: true),
            Reconstruct("Wireless Keyboard", "SKU-002"));

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(p => p.IsArchived);
    }

    [Fact]
    public async Task Handle_AccentedName_IsNotMatchedByUnaccentedTerm()
    {
        GivenProducts(MakeProduct("Cafأ© Latte Syrup", "SKU-001", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery("Cafe", null), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonBmpCharactersInName_MatchOnSurrogatePairBoundary()
    {
        GivenProducts(MakeProduct("Pallet \U0001F4E6 Wrapped", "SKU-001", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery("\U0001F4E6", null), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_DuplicateNames_ReturnsEveryMatchNotJustTheFirst()
    {
        GivenProducts(
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("Wireless Mouse", "SKU-002", "Globex"),
            MakeProduct("Wireless Mouse", "SKU-003", "Initech"));

        var result = await _handler.Handle(new SearchProductsQuery("Wireless Mouse", null), CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(p => p.Sku).Should().BeEquivalentTo("SKU-001", "SKU-002", "SKU-003");
    }

    [Fact]
    public async Task Handle_AlreadyCancelledToken_CompletesInsteadOfThrowing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        GivenProducts(MakeProduct("Wireless Mouse", "SKU-001", "Acme"));

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", null), cts.Token);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ExpiredProduct_StillAppearsInResults()
    {
        GivenProducts(Reconstruct("Wireless Mouse", "SKU-001", expiryDate: new DateTime(2000, 1, 1)));

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UtcTimestamps_ArePassedThroughWithoutNormalisation()
    {
        var expiry = new DateTime(2026, 3, 1, 23, 30, 0, DateTimeKind.Utc);
        GivenProducts(Reconstruct("Wireless Mouse", "SKU-001", expiryDate: expiry));

        var result = await HandlerWithRealMapper()
            .Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        var actual = result.Single().ExpiryDate!.Value;
        actual.Should().Be(expiry);
        actual.Kind.Should().Be(DateTimeKind.Utc);
        TimeZoneInfo.ConvertTimeFromUtc(actual, TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(3), "t", "t"))
            .Day.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NeverUpdatedProduct_CollapsesNullLastUpdatedAtToDateTimeMinValue()
    {
        GivenProducts(Reconstruct("Wireless Mouse", "SKU-001", lastUpdatedAt: null));

        var result = await HandlerWithRealMapper()
            .Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        var lastUpdated = result.Single().LastUpdatedAt;
        lastUpdated.Should().Be(DateTime.MinValue);

        var renderAtEasternOffset = () => new DateTimeOffset(lastUpdated, TimeSpan.FromHours(3));
        renderAtEasternOffset.Should().Throw<ArgumentOutOfRangeException>();
    }
}
