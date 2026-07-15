using Microsoft.AspNetCore.Http;

namespace WarehouseManagement.Api.Contracts;

public class UploadImageRequest
{
    public IFormFile? File { get; set; }
}
