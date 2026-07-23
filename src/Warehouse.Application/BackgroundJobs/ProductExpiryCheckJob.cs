namespace Warehouse.Application.BackgroundJobs;

using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public class ProductExpiryCheckJob
{
    private const int ExpiringSoonWindowDays = 30;

    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductExpiryCheckJob> _logger;

    public ProductExpiryCheckJob(IProductRepository productRepository, ILogger<ProductExpiryCheckJob> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task CheckExpiringProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);

        var candidates = products.Where(p => !p.IsArchived && p.ExpiryDate.HasValue).ToList();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var expiringSoonCutoff = now.AddDays(ExpiringSoonWindowDays);

        var expired = candidates.Where(p => p.ExpiryDate!.Value < now).ToList();
        var expiringSoon = candidates
            .Where(p => p.ExpiryDate!.Value >= now && p.ExpiryDate.Value <= expiringSoonCutoff)
            .ToList();

        foreach (var product in expired)
        {
            _logger.LogInformation(
                "Product {ProductName} is expired (expired on {ExpiryDate})",
                product.Name, product.ExpiryDate);
        }

        foreach (var product in expiringSoon)
        {
            _logger.LogInformation(
                "Product {ProductName} is expiring soon (expires on {ExpiryDate})",
                product.Name, product.ExpiryDate);
        }

        if (expired.Count == 0 && expiringSoon.Count == 0)
        {
            _logger.LogInformation("Expiry check completed: no expired or expiring-soon products found");
        }
        else
        {
            _logger.LogInformation(
                "Expiry check completed: {ExpiredCount} expired, {ExpiringSoonCount} expiring within 30 days",
                expired.Count, expiringSoon.Count);
        }
    }
}
