// ViewModels/RegisterViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class RegisterViewModel
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Password tidak sama.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
