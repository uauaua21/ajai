using System.ComponentModel.DataAnnotations;
namespace AJAI_Server.Dto;
public class CreateAlertDto
{
    [Required, EmailAddress, StringLength(254)]
    public string CameraEmail { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string AlertType { get; set; } = string.Empty;
    [Required, StringLength(1_400_000), Base64String]
    public string Base64Image { get; set; } = string.Empty;
}
