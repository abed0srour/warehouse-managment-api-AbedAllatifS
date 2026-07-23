using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Domain;

namespace Warehouse.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv"
    };

    private readonly IWarehouseFileRepository _warehouseFileRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IWarehouseFileRepository warehouseFileRepository,
        IFileStorageService fileStorageService,
        ILogger<FilesController> logger)
    {
        _warehouseFileRepository = warehouseFileRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    // POST /api/files/upload
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<WarehouseFile>> Upload(
        IFormFile file,
        [FromForm] string relatedEntityType,
        [FromForm] int relatedEntityId,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "A file is required." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File size cannot exceed 10 MB." });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Only image and common document file types are allowed." });
        }

        var uploadedByUid = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(uploadedByUid))
        {
            return Unauthorized(new { message = "Unable to determine the authenticated user's UID." });
        }

        await using var stream = file.OpenReadStream();
        var objectKey = await _fileStorageService.UploadAsync(stream, file.FileName, file.ContentType, cancellationToken);

        var warehouseFile = new WarehouseFile
        {
            ObjectKey = objectKey,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            UploadedByUid = uploadedByUid,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId
        };

        await _warehouseFileRepository.AddAsync(warehouseFile, cancellationToken);

        _logger.LogInformation(
            "File {FileId} uploaded by {UploadedByUid} for {RelatedEntityType} {RelatedEntityId} with object key {ObjectKey}",
            warehouseFile.Id, uploadedByUid, relatedEntityType, relatedEntityId, objectKey);

        return CreatedAtAction(nameof(Download), new { id = warehouseFile.Id }, warehouseFile);
    }

    // GET /api/files/{id}/download
    [Authorize(Policy = "AuthenticatedUser")]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var warehouseFile = await _warehouseFileRepository.GetByIdAsync(id, cancellationToken);
        if (warehouseFile == null)
        {
            return NotFound(new { message = $"File with ID {id} was not found." });
        }

        var stream = await _fileStorageService.DownloadAsync(warehouseFile.ObjectKey, cancellationToken);

        return File(stream, warehouseFile.ContentType, warehouseFile.FileName);
    }

    // DELETE /api/files/{id}
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var warehouseFile = await _warehouseFileRepository.GetByIdAsync(id, cancellationToken);
        if (warehouseFile == null)
        {
            return NotFound(new { message = $"File with ID {id} was not found." });
        }

        await _fileStorageService.DeleteAsync(warehouseFile.ObjectKey, cancellationToken);
        await _warehouseFileRepository.DeleteAsync(id, cancellationToken);

        _logger.LogInformation("File {FileId} deleted", id);

        return NoContent();
    }
}
