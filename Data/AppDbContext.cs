using Microsoft.EntityFrameworkCore;
using KetBanChoiChuoi.Models;
namespace KetBanChoiChuoi.Data;
public class AppDbContext : DbContext {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<Streak> Streaks { get; set; }
    public DbSet<OtpRecord> OtpRecords { get; set; }
    public DbSet<Pet> Pets { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Friendship>().HasOne(f => f.User1).WithMany().HasForeignKey(f => f.User1Id).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Friendship>().HasOne(f => f.User2).WithMany().HasForeignKey(f => f.User2Id).OnDelete(DeleteBehavior.Restrict);
    }
}
