using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Warehouse.Application;
using Warehouse.Infrastructure.Data.EfModels;
using Warehouse.Infrastructure.Mapping;

namespace Warehouse.Api.UnitTests.Queries;

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
