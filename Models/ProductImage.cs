using System;

namespace WarehouseManagement.Api.Models
{
    public class ProductImage
    {
        public Guid ProductId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
}