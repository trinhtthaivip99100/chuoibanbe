namespace KetBanChoiChuoi.Models;
public class Streak {
    public int Id { get; set; }
    public int FriendshipId { get; set; }
    public Friendship? Friendship { get; set; }
    public int CurrentStreakCount { get; set; } = 0;
    public int RecoveriesLeft { get; set; } = 5;
    public bool User1CheckedInToday { get; set; } = false;
    public bool User2CheckedInToday { get; set; } = false;
    public DateTimeOffset LastStreakDate { get; set; }
}
