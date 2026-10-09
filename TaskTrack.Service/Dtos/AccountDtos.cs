using System.ComponentModel.DataAnnotations;

namespace TaskTrack.Service.Dtos;

public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(128)] public string Password { get; set; } = string.Empty;
}

public class AccountUpdateRequest
{
    [StringLength(100)] public string? FullName { get; set; }
    [Range(0, 1)] public short? Role { get; set; }
}

public record AccountResponse(int AccountId, string FullName, string Email, short Role, DateTime CreatedDate);
