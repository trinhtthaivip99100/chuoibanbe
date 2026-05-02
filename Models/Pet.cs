namespace KetBanChoiChuoi.Models
{
    public class Pet
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int RequiredStreak { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
