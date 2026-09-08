// ViewModels/ForgotPasswordViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
