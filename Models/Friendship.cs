namespace KetBanChoiChuoi.Models;
public class Friendship {
    public int Id { get; set; }
    public int User1Id { get; set; }
    public User? User1 { get; set; }
    public int User2Id { get; set; }
    public User? User2 { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Status { get; set; } = 0; // 0 = Pending, 1 = Accepted
    public int ActionUserId { get; set; }
    public string ConnectionCode { get; set; } = "CH-" + System.Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
}
