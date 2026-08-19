using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;

namespace Warehouse.Api.IntegrationTests.Images;

public class UploadProductImageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly List<string> _writtenFiles = new();

    public UploadProductImageTests(CustomWebApplicationFactory factory)
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

    private string TrackExpectedPath(Guid id, string extension)
    {
        var path = Path.Combine("wwwroot", "uploads", $"{id}{extension}");
        _writtenFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task POST_ProductImage_ValidJpg_Succeeds()
    {
        var expectedPath = TrackExpectedPath(_factory.SeededProductOneId, ".jpg");
        using var form = BuildForm("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4 });

        var response = await _client.PostAsync($"/api/products/{_factory.SeededProductOneId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task POST_ProductImage_ValidPng_Succeeds()
    {
        var expectedPath = TrackExpectedPath(_factory.SeededProductTwoId, ".png");
        using var form = BuildForm("photo.png", "image/png", new byte[] { 1, 2, 3, 4 });

        var response = await _client.PostAsync($"/api/products/{_factory.SeededProductTwoId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task POST_ProductImage_TxtFile_Returns400()
    {
        using var form = BuildForm("notes.txt", "text/plain", new byte[] { 1, 2, 3, 4 });

        var response = await _client.PostAsync($"/api/products/{_factory.SeededProductOneId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_ProductImage_OversizedFile_Returns400()
    {
        var oversized = new byte[3 * 1024 * 1024];
        using var form = BuildForm("photo.jpg", "image/jpeg", oversized);

        var response = await _client.PostAsync($"/api/products/{_factory.SeededProductOneId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        foreach (var path in _writtenFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
