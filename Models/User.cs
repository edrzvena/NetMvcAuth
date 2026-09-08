using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;
        [Required]
        public string PasswordHash { get; set; }

        //Feature forgot password
        public string? ResetPasswordToken { get; set; }
        public DateTime? PasswordResetTokenExpireAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}