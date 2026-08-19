using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.DataModel.Response;
using Moq;
using Warehouse.Infrastructure.Storage;

namespace Warehouse.Api.UnitTests.FileUpload;

public class MinioStorageServiceTests
{
    private readonly Mock<IMinioClient> _minioClient = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly MinioStorageService _service;

    public MinioStorageServiceTests()
    {
        _configuration.Setup(c => c["MinIO:BucketName"]).Returns("warehouse-assets");

        _minioClient.Setup(c => c.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _minioClient.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse(HttpStatusCode.OK, string.Empty, new Dictionary<string, string>(), 0, string.Empty));

        _service = new MinioStorageService(_minioClient.Object, _configuration.Object);
    }

    [Fact]
    public async Task UploadAsync_ValidFile_GeneratesGuidPrefixedObjectKey()
    {
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var objectKey = await _service.UploadAsync(content, "photo.png", "image/png", CancellationToken.None);

        objectKey.Should().EndWith("-photo.png");
        objectKey.Should().MatchRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}-photo\.png$");
        _minioClient.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
