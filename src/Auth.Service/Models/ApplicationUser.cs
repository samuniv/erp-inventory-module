using Microsoft.AspNetCore.Identity;

namespace Auth.Service.Models;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties for refresh tokens
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; } = false;

    // Foreign key
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
