// Exercise 03 - unit test suite for the ten previously-untested query handlers.
//
// PROMPT TARGET vs REALITY: the prompt asked for tests covering "ProductService".
// No ProductService (or SupplierService) exists in this codebase - it is CQRS, so the
// equivalent units are MediatR query handlers. This suite covers all ten handlers that
// had zero test coverage. See ai-lab/notes/test-comparison.md for the full rationale.
//
// This is a flattened snapshot of eleven files that live in tests/Warehouse.Api.UnitTests/.
// One mechanical change was made when flattening: file-scoped namespaces became
// block-scoped, since C# permits only one file-scoped namespace per file. Each file's
// using directives stay INSIDE its own namespace block on purpose - hoisting them to the
// top of the file makes 'Product' and 'Supplier' ambiguous between Warehouse.Domain and
// Warehouse.Infrastructure.Data.EfModels. No test logic was altered.
//
// 'using Xunit;' is absent by design - the test project supplies it as a global using
// via <Using Include="Xunit" /> in Warehouse.Api.UnitTests.csproj.
//
// Result at time of capture: 144 tests, all passing (174 including the pre-existing
// tests in the same project).

// ============================================================================
// EfQueryHandlerTestBase.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Queries
{
    using AutoMapper;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Warehouse.Application;
    using Warehouse.Infrastructure.Data.EfModels;
    using Warehouse.Infrastructure.Mapping;

    /// <summary>
    /// Shared fixture for the five query handlers that talk to <see cref="WarehouseDbContext"/>
    /// directly instead of going through a repository interface, so they cannot be mocked.
    ///
    /// These run against the EF Core InMemory provider. That exercises the handler's own logic
    /// (filtering, ordering, paging arithmetic, grouping keys) but NOT SQL translation --
    /// InMemory evaluates everything client-side, so a LINQ shape that Npgsql cannot translate
    /// will still pass here. Translation is covered by the integration test project against a
    /// real database.
    /// </summary>
    public abstract class EfQueryHandlerTestBase : IDisposable
    {
        protected WarehouseDbContext Context { get; }
        protected IMapper Mapper { get; }

        protected EfQueryHandlerTestBase()
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseInMemoryDatabase($"warehouse-tests-{Guid.NewGuid()}")
                .Options;

            Context = new WarehouseDbContext(options);

            var configuration = new MapperConfiguration(
                cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                    cfg.AddProfile<EfMappingProfile>();
                },
                NullLoggerFactory.Instance);

            Mapper = configuration.CreateMapper();
        }

        protected static Product MakeProduct(
            string name,
            string sku,
            int quantity = 5,
            decimal price = 10m,
            string? supplierName = null,
            Guid? supplierId = null,
            DateTime? expiryDate = null,
            DateTime? createdAt = null,
            bool archived = false) => new()
            {
                Id = Guid.NewGuid(),
                Name = name,
                Sku = sku,
                Description = string.Empty,
                Price = price,
                QuantityInStock = quantity,
                SupplierName = supplierName,
                SupplierId = supplierId,
                ExpiryDate = expiryDate,
                IsArchived = archived,
                CreatedAt = createdAt ?? new DateTime(2026, 1, 1),
                LastUpdatedAt = null
            };

        protected static Supplier MakeSupplier(string name, string country = "US", bool active = true) => new()
        {
            SupplierId = Guid.NewGuid(),
            Name = name,
            Country = country,
            ContactEmail = $"{name.ToLowerInvariant()}@example.test",
            PhoneNumber = "+1-555-0100",
            IsActive = active
        };

        protected async Task SeedAsync(params object[] entities)
        {
            Context.AddRange(entities);
            await Context.SaveChangesAsync();
        }

        public void Dispose()
        {
            Context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

// ============================================================================
// GetAllProductsQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Products
{
    using System.Text;
    using System.Text.Json;
    using AutoMapper;
    using FluentAssertions;
    using Microsoft.Extensions.Caching.Distributed;
    using Moq;
    using Warehouse.Application.Products;
    using Warehouse.Application.Products.Queries;
    using Warehouse.Domain;

    public class GetAllProductsQueryHandlerTests
    {
        private readonly Mock<IProductRepository> _productRepository = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IDistributedCache> _cache = new();
        private readonly GetAllProductsQueryHandler _handler;

        public GetAllProductsQueryHandlerTests()
        {
            // Default to a cache MISS. Moq's default for byte[] is an empty array rather than
            // null, and GetStringAsync only null-checks -- so without this the handler would
            // receive "" and fail to deserialize it. See Handle_ZeroLengthCacheEntry_Throws.
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            _mapper.Setup(m => m.Map<IEnumerable<ProductViewModel>>(It.IsAny<object>()))
                .Returns((object source) => ((IEnumerable<Product>)source).Select(ToViewModel).ToList());

            _handler = new GetAllProductsQueryHandler(_productRepository.Object, _mapper.Object, _cache.Object);
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

        private static Product MakeProduct(
            string name,
            string sku,
            int quantity = 5,
            bool archived = false,
            DateTime? createdAt = null)
        {
            var product = Product.Create(name, sku, 10m, quantity);
            if (createdAt.HasValue)
                product.CreatedAt = createdAt.Value;
            if (archived)
                product.Archive();
            return product;
        }

        private void GivenProducts(params Product[] products) =>
            _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        // ---------- positive ----------

        [Fact]
        public async Task Handle_NoFilter_ReturnsAllProducts()
        {
            GivenProducts(MakeProduct("Mouse", "SKU-001"), MakeProduct("Keyboard", "SKU-002"));

            var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_NoFilter_OrdersByCreatedAtDescending()
        {
            GivenProducts(
                MakeProduct("Oldest", "SKU-001", createdAt: new DateTime(2024, 1, 1)),
                MakeProduct("Newest", "SKU-002", createdAt: new DateTime(2026, 1, 1)),
                MakeProduct("Middle", "SKU-003", createdAt: new DateTime(2025, 1, 1)));

            var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            result.Select(p => p.Name).Should().ContainInOrder("Newest", "Middle", "Oldest");
        }

        [Fact]
        public async Task Handle_OnlyAvailable_ExcludesArchivedProducts()
        {
            GivenProducts(
                MakeProduct("Active", "SKU-001"),
                MakeProduct("Archived", "SKU-002", archived: true));

            var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

            result.Should().ContainSingle().Which.Name.Should().Be("Active");
        }

        [Fact]
        public async Task Handle_OnlyAvailable_ExcludesOutOfStockProducts()
        {
            GivenProducts(
                MakeProduct("InStock", "SKU-001", quantity: 1),
                MakeProduct("OutOfStock", "SKU-002", quantity: 0));

            var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

            result.Should().ContainSingle().Which.Name.Should().Be("InStock");
        }

        [Fact]
        public async Task Handle_CacheMiss_WritesResultToCache()
        {
            GivenProducts(MakeProduct("Mouse", "SKU-001"));

            await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            _cache.Verify(
                c => c.SetAsync(
                    "products:all:False",
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_CacheHit_ReturnsCachedValueWithoutQueryingRepository()
        {
            var cached = new[] { new ProductViewModel { Name = "FromCache", Sku = "SKU-999" } };
            _cache.Setup(c => c.GetAsync("products:all:False", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached)));

            var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            result.Should().ContainSingle().Which.Name.Should().Be("FromCache");
            _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_OnlyAvailableFlag_UsesSeparateCacheKey()
        {
            GivenProducts(MakeProduct("Mouse", "SKU-001"));

            await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

            _cache.Verify(c => c.GetAsync("products:all:True", It.IsAny<CancellationToken>()), Times.Once);
            _cache.Verify(c => c.GetAsync("products:all:False", It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---------- negative ----------

        [Fact]
        public async Task Handle_EmptyRepository_ReturnsEmptyCollection()
        {
            GivenProducts();

            var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_AllProductsFilteredOut_ReturnsEmptyCollection()
        {
            GivenProducts(MakeProduct("Archived", "SKU-001", archived: true));

            var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_RepositoryThrows_PropagatesException()
        {
            _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));

            var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
        }

        [Fact]
        public async Task Handle_CacheReadThrows_PropagatesException()
        {
            // Infrastructure failure: Redis down on read. The handler has no try/catch,
            // so a cache outage takes the whole query down rather than degrading to the repository.
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("redis unavailable"));

            var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("redis unavailable");
        }

        [Fact]
        public async Task Handle_CacheWriteThrows_PropagatesException()
        {
            GivenProducts(MakeProduct("Mouse", "SKU-001"));
            _cache.Setup(c => c.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("redis unavailable"));

            var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Handle_CorruptCachePayload_ThrowsJsonException()
        {
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes("{ not valid json"));

            var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<JsonException>();
        }

        // ---------- edge cases ----------

        [Fact]
        public async Task Handle_MaxLengthProductName_IsPreservedIntact()
        {
            var longName = new string('X', 8000);
            GivenProducts(MakeProduct(longName, "SKU-001"));

            var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            result.Single().Name.Should().Be(longName).And.HaveLength(8000);
        }

        [Fact]
        public async Task Handle_MaxIntQuantity_IsPreservedWithoutOverflow()
        {
            GivenProducts(MakeProduct("Bulk", "SKU-001", quantity: int.MaxValue));

            var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

            result.Single().QuantityInStock.Should().Be(int.MaxValue);
        }

        [Fact]
        public async Task Handle_ProductsWithIdenticalCreatedAt_ReturnsAllOfThem()
        {
            var timestamp = new DateTime(2026, 1, 1);
            GivenProducts(
                MakeProduct("A", "SKU-001", createdAt: timestamp),
                MakeProduct("B", "SKU-002", createdAt: timestamp));

            var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_AbsentCacheEntry_FallsThroughToRepository()
        {
            GivenProducts(MakeProduct("Mouse", "SKU-001"));
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            result.Should().ContainSingle();
            _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ZeroLengthCacheEntry_ThrowsInsteadOfFallingBack()
        {
            // DEFECT: GetStringAsync only treats a NULL byte[] as a miss. A zero-length entry
            // -- which a truncated or evicted-mid-write Redis value can produce -- decodes to
            // "" and blows up in the deserializer, turning a degraded cache into a hard 500
            // instead of a fall-through to the repository.
            GivenProducts(MakeProduct("Mouse", "SKU-001"));
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<byte>());

            var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<JsonException>();
            _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}

// ============================================================================
// GetProductByIdQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Products
{
    using System.Text;
    using System.Text.Json;
    using AutoMapper;
    using FluentAssertions;
    using Microsoft.Extensions.Caching.Distributed;
    using Moq;
    using Warehouse.Application.Products;
    using Warehouse.Application.Products.Queries;
    using Warehouse.Domain;

    public class GetProductByIdQueryHandlerTests
    {
        private readonly Mock<IProductRepository> _productRepository = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IDistributedCache> _cache = new();
        private readonly GetProductByIdQueryHandler _handler;

        public GetProductByIdQueryHandlerTests()
        {
            // Default to a cache MISS -- see the note in GetAllProductsQueryHandlerTests:
            // Moq hands back an empty byte[], which GetStringAsync turns into "" rather than null.
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            _mapper.Setup(m => m.Map<ProductViewModel>(It.IsAny<Product>()))
                .Returns((Product p) => new ProductViewModel
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
                });

            _handler = new GetProductByIdQueryHandler(_productRepository.Object, _mapper.Object, _cache.Object);
        }

        // ---------- positive ----------

        [Fact]
        public async Task Handle_ExistingProduct_ReturnsMappedViewModel()
        {
            var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 5);
            _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Wireless Mouse");
            result.Sku.Should().Be("SKU-001");
            result.Price.Should().Be(19.99m);
        }

        [Fact]
        public async Task Handle_CacheMiss_WritesResultUnderIdScopedKey()
        {
            var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 5);
            _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

            _cache.Verify(
                c => c.SetAsync(
                    $"products:{product.Id}",
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_CacheHit_ReturnsCachedValueWithoutQueryingRepository()
        {
            var id = Guid.NewGuid();
            var cached = new ProductViewModel { Id = id, Name = "FromCache", Sku = "SKU-999" };
            _cache.Setup(c => c.GetAsync($"products:{id}", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached)));

            var result = await _handler.Handle(new GetProductByIdQuery(id), CancellationToken.None);

            result!.Name.Should().Be("FromCache");
            _productRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ArchivedProduct_IsStillReturned()
        {
            // Archived products remain readable by id; archiving hides them from availability
            // filters, it is not a delete.
            var product = Product.Create("Retired", "SKU-001", 10m, 5);
            product.Archive();
            _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.IsArchived.Should().BeTrue();
        }

        // ---------- negative ----------

        [Fact]
        public async Task Handle_MissingProduct_ReturnsNull()
        {
            _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            var result = await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

            result.Should().BeNull();
        }

        [Fact]
        public async Task Handle_MissingProduct_CachesTheNegativeResult()
        {
            // The handler caches "null" for 5 minutes. A product created moments after this
            // lookup stays invisible until that entry expires -- deliberate or not, it is the
            // current behaviour and worth pinning down.
            _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

            _cache.Verify(
                c => c.SetAsync(
                    It.IsAny<string>(),
                    It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "null"),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_EmptyGuid_ReturnsNullWhenNotFound()
        {
            _productRepository.Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            var result = await _handler.Handle(new GetProductByIdQuery(Guid.Empty), CancellationToken.None);

            result.Should().BeNull();
            _productRepository.Verify(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_RepositoryThrows_PropagatesException()
        {
            _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));

            var act = async () => await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
        }

        [Fact]
        public async Task Handle_CacheReadThrows_PropagatesException()
        {
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("redis unavailable"));

            var act = async () => await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("redis unavailable");
        }

        [Fact]
        public async Task Handle_CorruptCachePayload_ThrowsJsonException()
        {
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes("<<<not json>>>"));

            var act = async () => await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

            await act.Should().ThrowAsync<JsonException>();
        }

        // ---------- edge cases ----------

        [Fact]
        public async Task Handle_MaxLengthDescription_SurvivesSerializationRoundTrip()
        {
            var product = Product.Create("Mouse", "SKU-001", 10m, 5);
            product.Description = new string('D', 10_000);
            _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

            result!.Description.Should().HaveLength(10_000);
        }

        [Fact]
        public async Task Handle_UnicodeAndControlCharactersInName_AreSerializedSafely()
        {
            var product = Product.Create("Ünïcødé \"quoted\" \\ backslash", "SKU-001", 10m, 5);
            _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

            result!.Name.Should().Be("Ünïcødé \"quoted\" \\ backslash");
        }

        [Fact]
        public async Task Handle_ExpiryDateAtYearBoundary_IsReturnedUnshifted()
        {
            // The codebase stores DateTimeKind.Unspecified throughout. Serializing to cache and
            // back must not shift the instant across the year boundary.
            var product = Product.Create("Milk", "SKU-001", 10m, 5);
            product.ExpiryDate = DateTime.SpecifyKind(new DateTime(2025, 12, 31, 23, 59, 59), DateTimeKind.Unspecified);
            _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

            result!.ExpiryDate.Should().Be(new DateTime(2025, 12, 31, 23, 59, 59));
            result.ExpiryDate!.Value.Year.Should().Be(2025);
        }

        [Fact]
        public async Task Handle_CachedPayloadOfLiteralNull_ReturnsNull()
        {
            _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes("null"));

            var result = await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

            result.Should().BeNull();
            _productRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}

// ============================================================================
// GetAllSuppliersQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Suppliers
{
    using AutoMapper;
    using FluentAssertions;
    using Moq;
    using Warehouse.Application.Suppliers;
    using Warehouse.Application.Suppliers.Queries;
    using Warehouse.Domain;

    public class GetAllSuppliersQueryHandlerTests
    {
        private readonly Mock<ISupplierRepository> _supplierRepository = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly GetAllSuppliersQueryHandler _handler;

        public GetAllSuppliersQueryHandlerTests()
        {
            _mapper.Setup(m => m.Map<IEnumerable<SupplierViewModel>>(It.IsAny<object>()))
                .Returns((object source) => ((IEnumerable<Supplier>)source).Select(s => new SupplierViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Country = s.Country,
                    ContactEmail = s.ContactEmail,
                    PhoneNumber = s.PhoneNumber,
                    IsActive = s.IsActive
                }).ToList());

            _handler = new GetAllSuppliersQueryHandler(_supplierRepository.Object, _mapper.Object);
        }

        private void GivenSuppliers(params Supplier[] suppliers) =>
            _supplierRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(suppliers);

        // ---------- positive ----------

        [Fact]
        public async Task Handle_ReturnsAllSuppliers()
        {
            GivenSuppliers(
                new Supplier { Name = "Acme", Country = "US" },
                new Supplier { Name = "Globex", Country = "DE" });

            var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
            result.Select(s => s.Name).Should().BeEquivalentTo("Acme", "Globex");
        }

        [Fact]
        public async Task Handle_MapsAllSupplierFields()
        {
            var supplier = new Supplier
            {
                Name = "Acme",
                Country = "US",
                ContactEmail = "sales@acme.test",
                PhoneNumber = "+1-555-0100",
                IsActive = true
            };
            GivenSuppliers(supplier);

            var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            var vm = result.Single();
            vm.Id.Should().Be(supplier.Id);
            vm.Name.Should().Be("Acme");
            vm.Country.Should().Be("US");
            vm.ContactEmail.Should().Be("sales@acme.test");
            vm.PhoneNumber.Should().Be("+1-555-0100");
            vm.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_IncludesInactiveSuppliers()
        {
            // This query applies no filtering -- deactivated suppliers are still listed.
            GivenSuppliers(
                new Supplier { Name = "Active", IsActive = true },
                new Supplier { Name = "Inactive", IsActive = false });

            var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
            result.Should().Contain(s => !s.IsActive);
        }

        // ---------- negative ----------

        [Fact]
        public async Task Handle_NoSuppliers_ReturnsEmptyCollection()
        {
            GivenSuppliers();

            var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_RepositoryThrows_PropagatesException()
        {
            _supplierRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));

            var act = async () => await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
        }

        [Fact]
        public async Task Handle_PassesCancellationTokenToRepository()
        {
            using var cts = new CancellationTokenSource();
            GivenSuppliers();

            await _handler.Handle(new GetAllSuppliersQuery(), cts.Token);

            _supplierRepository.Verify(r => r.GetAllAsync(cts.Token), Times.Once);
        }

        // ---------- edge cases ----------

        [Fact]
        public async Task Handle_MaxLengthSupplierName_IsPreservedIntact()
        {
            var longName = new string('S', 4000);
            GivenSuppliers(new Supplier { Name = longName, Country = "US" });

            var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            result.Single().Name.Should().Be(longName).And.HaveLength(4000);
        }

        [Fact]
        public async Task Handle_LargeSupplierSet_ReturnsEveryRow()
        {
            var suppliers = Enumerable.Range(0, 5_000)
                .Select(i => new Supplier { Name = $"Supplier {i}", Country = "US" })
                .ToArray();
            GivenSuppliers(suppliers);

            var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            result.Should().HaveCount(5_000);
        }

        [Fact]
        public async Task Handle_UnicodeCountryNames_ArePreserved()
        {
            GivenSuppliers(new Supplier { Name = "Zürich Imports", Country = "Côte d'Ivoire" });

            var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

            result.Single().Country.Should().Be("Côte d'Ivoire");
        }
    }
}

// ============================================================================
// GetSupplierByIdQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Suppliers
{
    using AutoMapper;
    using FluentAssertions;
    using Moq;
    using Warehouse.Application.Suppliers;
    using Warehouse.Application.Suppliers.Queries;
    using Warehouse.Domain;

    public class GetSupplierByIdQueryHandlerTests
    {
        private readonly Mock<ISupplierRepository> _supplierRepository = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly GetSupplierByIdQueryHandler _handler;

        public GetSupplierByIdQueryHandlerTests()
        {
            _mapper.Setup(m => m.Map<SupplierViewModel>(It.IsAny<Supplier>()))
                .Returns((Supplier s) => new SupplierViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Country = s.Country,
                    ContactEmail = s.ContactEmail,
                    PhoneNumber = s.PhoneNumber,
                    IsActive = s.IsActive
                });

            _handler = new GetSupplierByIdQueryHandler(_supplierRepository.Object, _mapper.Object);
        }

        // ---------- positive ----------

        [Fact]
        public async Task Handle_ExistingSupplier_ReturnsMappedViewModel()
        {
            var supplier = new Supplier { Name = "Acme", Country = "US", IsActive = true };
            _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(supplier);

            var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.Id.Should().Be(supplier.Id);
            result.Name.Should().Be("Acme");
            result.Country.Should().Be("US");
        }

        [Fact]
        public async Task Handle_InactiveSupplier_IsStillReturned()
        {
            var supplier = new Supplier { Name = "Retired", IsActive = false };
            _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(supplier);

            var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_QueriesRepositoryWithRequestedId()
        {
            var id = Guid.NewGuid();
            _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Supplier?)null);

            await _handler.Handle(new GetSupplierByIdQuery(id), CancellationToken.None);

            _supplierRepository.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---------- negative ----------

        [Fact]
        public async Task Handle_MissingSupplier_ReturnsNull()
        {
            _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Supplier?)null);

            var result = await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None);

            result.Should().BeNull();
        }

        [Fact]
        public async Task Handle_MissingSupplier_DoesNotInvokeMapper()
        {
            _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Supplier?)null);

            await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None);

            _mapper.Verify(m => m.Map<SupplierViewModel>(It.IsAny<Supplier>()), Times.Never);
        }

        [Fact]
        public async Task Handle_EmptyGuid_ReturnsNullWhenNotFound()
        {
            _supplierRepository.Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Supplier?)null);

            var result = await _handler.Handle(new GetSupplierByIdQuery(Guid.Empty), CancellationToken.None);

            result.Should().BeNull();
        }

        [Fact]
        public async Task Handle_RepositoryThrows_PropagatesException()
        {
            _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));

            var act = async () => await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
        }

        [Fact]
        public async Task Handle_CancelledToken_PropagatesOperationCanceled()
        {
            _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var act = async () => await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), new CancellationToken(true));

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // ---------- edge cases ----------

        [Fact]
        public async Task Handle_MaxLengthContactEmail_IsPreservedIntact()
        {
            var longEmail = new string('e', 300) + "@example.test";
            var supplier = new Supplier { Name = "Acme", ContactEmail = longEmail };
            _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(supplier);

            var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

            result!.ContactEmail.Should().Be(longEmail);
        }

        [Fact]
        public async Task Handle_DefaultStringFields_MapToEmptyNotNull()
        {
            var supplier = new Supplier { Name = "Acme" };
            _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(supplier);

            var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

            result!.Country.Should().BeEmpty();
            result.ContactEmail.Should().BeEmpty();
            result.PhoneNumber.Should().BeEmpty();
        }
    }
}

// ============================================================================
// GetInventoryDashboardQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Inventory
{
    using FluentAssertions;
    using Moq;
    using Warehouse.Application.Inventory.Queries;
    using Warehouse.Domain;

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
}

// ============================================================================
// GetPagedProductsQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Queries
{
    using FluentAssertions;
    using Warehouse.Application.Products.Queries;
    using Warehouse.Infrastructure.Queries;

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
}

// ============================================================================
// GetTotalProductCountQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Queries
{
    using FluentAssertions;
    using Warehouse.Application.Products.Queries;
    using Warehouse.Infrastructure.Queries;

    public class GetTotalProductCountQueryHandlerTests : EfQueryHandlerTestBase
    {
        private GetTotalProductCountQueryHandler CreateHandler() => new(Context);

        // ---------- positive ----------

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
            // The count is unfiltered, so it does not match what the "only available" product
            // list returns. Consumers pairing the two will see inconsistent totals.
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

        // ---------- negative ----------

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
            // Infrastructure failure: the scoped DbContext is gone underneath the handler.
            var handler = CreateHandler();
            Context.Dispose();

            var act = async () => await handler.Handle(new GetTotalProductCountQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        // ---------- edge cases ----------

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
            // Documents the contract rather than the data: the query returns Int32, so a
            // catalogue larger than int.MaxValue rows could not be represented. CountAsync
            // would throw on overflow rather than wrap, but the ceiling is worth recording.
            await SeedAsync(MakeProduct("A", "SKU-0001"));

            var result = await CreateHandler().Handle(new GetTotalProductCountQuery(), CancellationToken.None);

            result.Should().BeOfType(typeof(int));
            result.Should().BeLessThanOrEqualTo(int.MaxValue);
        }
    }
}

// ============================================================================
// GetProductsBySupplierQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Queries
{
    using FluentAssertions;
    using Warehouse.Application.Products.Queries;
    using Warehouse.Infrastructure.Queries;

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
}

// ============================================================================
// GetProductsGroupedByExpiryYearQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Queries
{
    using FluentAssertions;
    using Warehouse.Application.Products.Queries;
    using Warehouse.Infrastructure.Queries;

    public class GetProductsGroupedByExpiryYearQueryHandlerTests : EfQueryHandlerTestBase
    {
        private GetProductsGroupedByExpiryYearQueryHandler CreateHandler() => new(Context);

        private static DateTime Unspecified(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
            DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, second), DateTimeKind.Unspecified);

        // ---------- positive ----------

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

        // ---------- negative ----------

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

        // ---------- edge cases: timezone / year boundary ----------

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
            // The grouping key is the raw stored DateTime's Year. Nothing converts to UTC or to
            // the request's local zone, so a product expiring at 23:30 on Dec 31 is filed under
            // the earlier year even though it is already January in any zone east of the
            // storage zone. This is consistent with the codebase storing DateTimeKind.Unspecified
            // everywhere, but it means "expiring in 2025" is zone-relative and undefined.
            var lateOnNewYearsEve = Unspecified(2025, 12, 31, 23, 30, 0);
            await SeedAsync(MakeProduct("Edge", "SKU-0001", expiryDate: lateOnNewYearsEve));

            var result = await CreateHandler().Handle(new GetProductsGroupedByExpiryYearQuery(), CancellationToken.None);

            result.Single().Year.Should().Be(2025);
            result.Single().Products.Single().ExpiryDate.Should().Be(lateOnNewYearsEve);
        }

        [Fact]
        public async Task Handle_UtcKindExpiryDate_IsGroupedByItsRawYearNotConverted()
        {
            // Same point, made with an explicitly-UTC value: 2025-12-31T23:30Z is 2026 in CET,
            // but the handler still reports 2025 because it never converts.
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
}

// ============================================================================
// GetProductsGroupedByExpiryAndCountryQueryHandlerTests.cs
// ============================================================================

namespace Warehouse.Api.UnitTests.Queries
{
    using FluentAssertions;
    using Warehouse.Application.Products.Queries;
    using Warehouse.Infrastructure.Queries;

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
}

