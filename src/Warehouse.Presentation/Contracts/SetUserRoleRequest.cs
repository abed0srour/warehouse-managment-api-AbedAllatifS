using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Api.Contracts;

public class SetUserRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}
