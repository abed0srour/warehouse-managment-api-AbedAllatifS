using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Images;

public class UploadProductImageEndpointTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly List<string> _writtenFiles = new();

    public UploadProductImageEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static MultipartFormDataContent BuildForm(string fileName, string contentType, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private string TrackPath(Guid id, string extension)
    {
        var path = Path.Combine("wwwroot", "uploads", $"{id}{extension}");
        _writtenFiles.Add(path);
        return path;
    }

    private async Task<Guid> CreateProductAsync()
    {
        var request = new CreateProductRequest
        {
            Name = "Product With Image",
            SKU = $"SKU-{Guid.NewGuid():N}".Substring(0, 12),
            Description = "For image upload tests",
            Price = 12.50m,
            QuantityInStock = 3
        };

        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task POST_Image_ValidJpg_Returns200()
    {
        var id = await CreateProductAsync();
        TrackPath(id, ".jpg");
        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_Image_ReturnsJsonBodyWithMessageAndPath()
    {
        var id = await CreateProductAsync();
        TrackPath(id, ".jpg");
        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("message").GetString().Should().Be("Image uploaded successfully.");
        document.RootElement.GetProperty("path").GetString().Should().Be($"/uploads/{id}.jpg");
    }

    [Fact]
    public async Task POST_Image_EchoesCorrelationIdHeader()
    {
        var id = await CreateProductAsync();
        TrackPath(id, ".jpg");
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/products/{id}/image")
        {
            Content = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 })
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be(correlationId);
    }

    [Fact]
    public async Task POST_Image_WritesFileToDisk()
    {
        var id = await CreateProductAsync();
        var expectedPath = TrackPath(id, ".jpg");
        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });

        await _client.PostAsync($"/api/products/{id}/image", form);

        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task POST_Image_StoredBytesAreByteForByteIdentical()
    {

        var id = await CreateProductAsync();
        var expectedPath = TrackPath(id, ".png");
        var payload = new byte[64 * 1024];
        new Random(20260804).NextBytes(payload);
        using var form = BuildForm("photo.png", "image/png", payload);

        await _client.PostAsync($"/api/products/{id}/image", form);

        var stored = await File.ReadAllBytesAsync(expectedPath);
        stored.Should().Equal(payload);
    }

    [Fact]
    public async Task POST_Image_FileIsNamedAfterTheProductId()
    {
        var id = await CreateProductAsync();
        var expectedPath = TrackPath(id, ".png");
        using var form = BuildForm("completely-unrelated-name.png", "image/png", new byte[] { 1, 2, 3 });

        await _client.PostAsync($"/api/products/{id}/image", form);

        File.Exists(expectedPath).Should().BeTrue();
        Path.GetFileNameWithoutExtension(expectedPath).Should().Be(id.ToString());
    }

    [Fact]
    public async Task POST_Image_UppercaseExtension_IsNormalisedToLowercase()
    {
        var id = await CreateProductAsync();
        var expectedPath = TrackPath(id, ".jpg");
        using var form = BuildForm("PHOTO.JPG", "image/jpeg", new byte[] { 1, 2, 3 });

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task POST_Image_SecondUploadOverwritesTheFirst()
    {
        var id = await CreateProductAsync();
        var expectedPath = TrackPath(id, ".jpg");

        using (var first = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }))
        {
            await _client.PostAsync($"/api/products/{id}/image", first);
        }

        var replacement = new byte[] { 9, 9 };
        using (var second = BuildForm("photo.jpg", "image/jpeg", replacement))
        {
            await _client.PostAsync($"/api/products/{id}/image", second);
        }

        var stored = await File.ReadAllBytesAsync(expectedPath);
        stored.Should().Equal(replacement);
    }

    [Fact]
    public async Task POST_Image_DoesNotCreateAProductImagesRow()
    {

        var id = await CreateProductAsync();
        TrackPath(id, ".jpg");
        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });

        await _client.PostAsync($"/api/products/{id}/image", form);

        using var db = _factory.CreateDbContext();
        (await db.Productimages.AsNoTracking().AnyAsync(i => i.ProductId == id)).Should().BeFalse();
    }

    [Fact]
    public async Task POST_Image_DoesNotModifyTheProductRow()
    {
        var id = await CreateProductAsync();
        TrackPath(id, ".jpg");

        using (var db = _factory.CreateDbContext())
        {
            (await db.Products.AsNoTracking().SingleAsync(p => p.Id == id)).LastUpdatedAt.Should().BeNull();
        }

        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });
        await _client.PostAsync($"/api/products/{id}/image", form);

        using var after = _factory.CreateDbContext();
        var product = await after.Products.AsNoTracking().SingleAsync(p => p.Id == id);
        product.LastUpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task POST_Image_UnknownProduct_Returns404()
    {
        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });

        var response = await _client.PostAsync($"/api/products/{Guid.NewGuid()}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_Image_UnknownProduct_WritesNoFile()
    {
        var unknownId = Guid.NewGuid();
        var path = TrackPath(unknownId, ".jpg");
        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });

        await _client.PostAsync($"/api/products/{unknownId}/image", form);

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task POST_Image_DisallowedExtension_Returns400AndWritesNoFile()
    {
        var id = await CreateProductAsync();
        var path = TrackPath(id, ".txt");
        using var form = BuildForm("notes.txt", "text/plain", new byte[] { 1, 2, 3, 4 });

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task POST_Image_OversizedFile_Returns400AndWritesNoFile()
    {
        var id = await CreateProductAsync();
        var path = TrackPath(id, ".jpg");
        var oversized = new byte[(2 * 1024 * 1024) + 1];
        using var form = BuildForm("photo.jpg", "image/jpeg", oversized);

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task POST_Image_EmptyFile_Returns400()
    {
        var id = await CreateProductAsync();
        TrackPath(id, ".jpg");
        using var form = BuildForm("photo.jpg", "image/jpeg", Array.Empty<byte>());

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Image_ContentIsNotValidatedAgainstItsExtension()
    {

        var id = await CreateProductAsync();
        var expectedPath = TrackPath(id, ".jpg");
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("#!/bin/sh\necho definitely not a jpeg\n");
        using var form = BuildForm("payload.jpg", "image/jpeg", notAnImage);

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await File.ReadAllBytesAsync(expectedPath)).Should().Equal(notAnImage);
    }

    [Fact]
    public async Task POST_Image_DeclaredContentTypeIsIgnored()
    {
        var id = await CreateProductAsync();
        var expectedPath = TrackPath(id, ".png");
        using var form = BuildForm("photo.png", "application/octet-stream", new byte[] { 1, 2, 3 });

        var response = await _client.PostAsync($"/api/products/{id}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(expectedPath).Should().BeTrue();
    }

    public void Dispose()
    {
        foreach (var path in _writtenFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}
