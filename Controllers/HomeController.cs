using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KetBanChoiChuoi.Controllers;

public class StreakViewModel {
    public int FriendshipId { get; set; }
    public string ConnectionCode { get; set; }
    public KetBanChoiChuoi.Models.User Friend { get; set; }
    public KetBanChoiChuoi.Models.Streak Streak { get; set; }
    public bool ICheckedIn { get; set; }
    public bool FriendCheckedIn { get; set; }
}

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        var friendships = await _db.Friendships
            .Include(f => f.User1).Include(f => f.User2)
            .Where(f => (f.User1Id == myId || f.User2Id == myId) && f.Status == 1)
            .ToListAsync();

        var today = System.DateTime.UtcNow.Date;
        var viewModels = new List<StreakViewModel>();

        foreach(var f in friendships)
        {
            var s = await _db.Streaks.FirstOrDefaultAsync(x => x.FriendshipId == f.Id);
            if (s == null) continue;

            if (s.LastStreakDate.Date < today)
            {
                if (!(s.User1CheckedInToday && s.User2CheckedInToday) && s.CurrentStreakCount > 0)
                {
                    s.RecoveriesLeft -= 1;
                    if (s.RecoveriesLeft < 0) { s.CurrentStreakCount = 0; s.RecoveriesLeft = 5; }
                }
                s.User1CheckedInToday = false;
                s.User2CheckedInToday = false;
                s.LastStreakDate = today;
            }

            bool isUser1 = f.User1Id == myId;
            viewModels.Add(new StreakViewModel {
                FriendshipId = f.Id,
                ConnectionCode = f.ConnectionCode,
                Friend = isUser1 ? f.User2 : f.User1,
                Streak = s,
                ICheckedIn = isUser1 ? s.User1CheckedInToday : s.User2CheckedInToday,
                FriendCheckedIn = isUser1 ? s.User2CheckedInToday : s.User1CheckedInToday
            });
        }
        await _db.SaveChangesAsync();

        ViewBag.Pets = await _db.Pets.OrderByDescending(p => p.RequiredStreak).ToListAsync();

        return View(viewModels);
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(int friendshipId)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var f = await _db.Friendships.FirstOrDefaultAsync(x => x.Id == friendshipId && x.Status == 1 && (x.User1Id == myId || x.User2Id == myId));
        if (f == null) return RedirectToAction("Index");

        var s = await _db.Streaks.FirstOrDefaultAsync(x => x.FriendshipId == f.Id);
        if (s == null) return RedirectToAction("Index");

        bool isUser1 = f.User1Id == myId;
        if (isUser1) s.User1CheckedInToday = true;
        else s.User2CheckedInToday = true;

        if (s.User1CheckedInToday && s.User2CheckedInToday) s.CurrentStreakCount += 1;

        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> CancelStreak(int friendshipId)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var friendship = await _db.Friendships.FindAsync(friendshipId);
        
        if (friendship != null && (friendship.User1Id == myId || friendship.User2Id == myId))
        {
            var streak = await _db.Streaks.FirstOrDefaultAsync(s => s.FriendshipId == friendshipId);
            if (streak != null) _db.Streaks.Remove(streak);
            
            _db.Friendships.Remove(friendship);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Hủy chuỗi thành công!";
        }
        else 
        {
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi hủy chuỗi.";
        }
        return RedirectToAction("Index");
    }
}
