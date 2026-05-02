namespace KetBanChoiChuoi.Models;
public class User {
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; } = false;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int RoleId { get; set; } = 2; // 1 = Admin, 2 = User
    public bool IsLocked { get; set; } = false;
    public string? LockReason { get; set; }
    public System.DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
}
