// ViewModels/ResetPasswordViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Password tidak sama.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
