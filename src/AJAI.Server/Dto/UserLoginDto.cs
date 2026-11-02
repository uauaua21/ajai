using System.ComponentModel.DataAnnotations;
namespace AJAI_Server.Dto;
public class UserLoginDto
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(72)]
    public string Password { get; set; } = string.Empty;
}
