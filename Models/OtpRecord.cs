namespace KetBanChoiChuoi.Models;
public class OtpRecord {
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset ExpiryTime { get; set; }
    public string Purpose { get; set; } = string.Empty; // "Register" or "ResetPassword"
    public bool IsUsed { get; set; } = false;
}
