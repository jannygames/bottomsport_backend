using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bottomsport_backend.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    public string Role { get; set; } = "user";
    
    public float? Balance { get; set; }
    
    public DateTime RegistrationDate { get; set; } = DateTime.Now;
}

public class RegisterUserRequest
{
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [StringLength(50)]
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
} 