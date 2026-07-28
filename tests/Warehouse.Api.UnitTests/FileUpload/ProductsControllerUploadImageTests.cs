using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Queries;
using Warehouse.Application.Resources;
using Warehouse.Presentation.Controllers;

namespace Warehouse.Api.UnitTests.FileUpload;

public class ProductsControllerUploadImageTests : IDisposable
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _localizer = new();
    private readonly Mock<ILogger<ProductsController>> _logger = new();
    private readonly ProductsController _controller;
    private readonly List<string> _writtenFiles = new();

    public ProductsControllerUploadImageTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetProductByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductViewModel { Id = Guid.NewGuid(), Name = "Wireless Mouse", Sku = "SKU-001" });

        _controller = new ProductsController(_mediator.Object, _localizer.Object, _logger.Object);
    }

    private static Mock<IFormFile> MakeFormFile(string fileName, long length)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return file;
    }

    private string TrackExpectedPath(Guid id, string extension)
    {
        var path = Path.Combine("wwwroot", "uploads", $"{id}{extension}");
        _writtenFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task UploadImage_ValidJpg_Succeeds()
    {
        var id = Guid.NewGuid();
        var expectedPath = TrackExpectedPath(id, ".jpg");
        var file = MakeFormFile("photo.jpg", 1024);

        var result = await _controller.UploadImage(id, file.Object, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadImage_ValidPng_Succeeds()
    {
        var id = Guid.NewGuid();
        var expectedPath = TrackExpectedPath(id, ".png");
        var file = MakeFormFile("photo.png", 1024);

        var result = await _controller.UploadImage(id, file.Object, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("notes.txt")]
    public async Task UploadImage_BadExtension_ReturnsBadRequest(string fileName)
    {
        var file = MakeFormFile(fileName, 1024);

        var result = await _controller.UploadImage(Guid.NewGuid(), file.Object, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadImage_OverSizeLimit_ReturnsBadRequest()
    {
        var file = MakeFormFile("photo.jpg", 3 * 1024 * 1024);

        var result = await _controller.UploadImage(Guid.NewGuid(), file.Object, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
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
