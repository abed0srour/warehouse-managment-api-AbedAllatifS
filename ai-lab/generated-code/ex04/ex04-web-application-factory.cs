using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using EfProduct = Warehouse.Infrastructure.Data.EfModels.Product;
using EfSupplier = Warehouse.Infrastructure.Data.EfModels.Supplier;
using WarehouseDbContext = Warehouse.Infrastructure.Data.EfModels.WarehouseDbContext;

namespace Warehouse.Api.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public Guid SeededProductOneId { get; } = Guid.NewGuid();
    public Guid SeededProductTwoId { get; } = Guid.NewGuid();
    public Guid SeededSupplierOneId { get; } = Guid.NewGuid();
    public Guid SeededSupplierTwoId { get; } = Guid.NewGuid();

    public const string SeededProductOneSku = "SKU-001";
    public const string SeededProductTwoSku = "SKU-002";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WarehouseDbContext>>();
            services.RemoveAll<IDbContextFactory<WarehouseDbContext>>();
            services.RemoveAll<WarehouseDbContext>();

            services.AddDbContextFactory<WarehouseDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.AddScoped<WarehouseDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<WarehouseDbContext>>().CreateDbContext());

            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            SeedTestData(context);
        });
    }

    public WarehouseDbContext CreateDbContext() =>
        Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>().CreateDbContext();

    private void SeedTestData(WarehouseDbContext context)
    {
        context.Database.EnsureCreated();

        var supplierOne = new EfSupplier
        {
            SupplierId = SeededSupplierOneId,
            Name = "Acme Corp",
            Country = "USA",
            ContactEmail = "contact@acme.com",
            PhoneNumber = "555-0100",
            IsActive = true
        };

        var supplierTwo = new EfSupplier
        {
            SupplierId = SeededSupplierTwoId,
            Name = "Globex Supplies",
            Country = "Canada",
            ContactEmail = "sales@globex.com",
            PhoneNumber = "555-0200",
            IsActive = true
        };

        context.Suppliers.AddRange(supplierOne, supplierTwo);

        context.Products.AddRange(
            new EfProduct
            {
                Id = SeededProductOneId,
                Name = "Wireless Mouse",
                Sku = SeededProductOneSku,
                Description = "A wireless mouse",
                Price = 19.99m,
                QuantityInStock = 50,
                SupplierName = supplierOne.Name,
                SupplierId = supplierOne.SupplierId,
                IsArchived = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            },
            new EfProduct
            {
                Id = SeededProductTwoId,
                Name = "USB Cable",
                Sku = SeededProductTwoSku,
                Description = "A USB-C cable",
                Price = 9.99m,
                QuantityInStock = 200,
                SupplierName = supplierTwo.Name,
                SupplierId = supplierTwo.SupplierId,
                IsArchived = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });

        context.SaveChanges();
    }
}
