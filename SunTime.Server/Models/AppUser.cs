using Microsoft.AspNetCore.Identity;

namespace SunTime.Server.Models;

public enum UserRole
{
    User,
    Admin,
    SuperAdmin
}

public enum UserStatus
{
    PendingApproval,
    Active,
    Rejected,
    Suspended
}

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalId { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public UserStatus Status { get; set; } = UserStatus.PendingApproval;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
