using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
namespace WarehouseManagement.Api.Contracts;

public class UploadImageRequest
{
    [Required]
    public IFormFile? File { get; set; }
}
