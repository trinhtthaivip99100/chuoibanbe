using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using KetBanChoiChuoi.Hubs;

namespace KetBanChoiChuoi.Controllers;

[Authorize]
public class FriendController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    public FriendController(AppDbContext db, IHubContext<NotificationHub> hubContext) 
    { 
        _db = db; 
        _hubContext = hubContext;
    }

    [HttpGet]
    public IActionResult Search()
    {
        return View();
    }

    [HttpGet]
    public IActionResult SearchAjax(string q)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (string.IsNullOrEmpty(q)) return Json(new List<object>());

        var users = _db.Users
            .Where(u => u.Id != myId && (u.DisplayName.Contains(q) || u.Username.Contains(q)))
            .Take(3)
            .Select(u => new {
                id = u.Id,
                displayName = u.DisplayName,
                username = u.Username,
                avatarUrl = u.AvatarUrl,
                friendship = _db.Friendships.FirstOrDefault(f => (f.User1Id == myId && f.User2Id == u.Id) || (f.User1Id == u.Id && f.User2Id == myId))
            })
            .ToList()
            .Select(x => new {
                id = x.id,
                displayName = x.displayName,
                username = x.username,
                avatarUrl = x.avatarUrl,
                status = x.friendship != null ? x.friendship.Status : -1,
                actionUserId = x.friendship != null ? x.friendship.ActionUserId : -1
            })
            .ToList();
        return Json(users);
    }

    [HttpGet]
    public IActionResult GetPendingCount()
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        int count = _db.Friendships.Count(f => (f.User1Id == myId || f.User2Id == myId) && f.Status == 0 && f.ActionUserId != myId);
        return Json(new { count = count });
    }

    [HttpPost]
    public async Task<IActionResult> SendRequest(int targetUserId)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var existing = await _db.Friendships.FirstOrDefaultAsync(f => (f.User1Id == myId && f.User2Id == targetUserId) || (f.User1Id == targetUserId && f.User2Id == myId));
        if (existing == null)
        {
            var f = new KetBanChoiChuoi.Models.Friendship {
                User1Id = myId, User2Id = targetUserId, Status = 0, ActionUserId = myId
            };
            _db.Friendships.Add(f);
            await _db.SaveChangesAsync();
            
            // Gửi thông báo realtime đến targetUserId
            await _hubContext.Clients.User(targetUserId.ToString()).SendAsync("ReceiveNotification");
        }
        return RedirectToAction("Search");
    }

    [HttpPost]
    public async Task<IActionResult> CancelRequest(int targetUserId)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var f = await _db.Friendships.FirstOrDefaultAsync(f => ((f.User1Id == myId && f.User2Id == targetUserId) || (f.User1Id == targetUserId && f.User2Id == myId)) && f.Status == 0 && f.ActionUserId == myId);
        if (f != null)
        {
            _db.Friendships.Remove(f);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Search");
    }

    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var pending = await _db.Friendships
            .Include(f => f.User1).Include(f => f.User2)
            .Where(f => (f.User1Id == myId || f.User2Id == myId) && f.Status == 0 && f.ActionUserId != myId)
            .ToListAsync();
        return View(pending);
    }

    [HttpPost]
    public async Task<IActionResult> Accept(int id)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var f = await _db.Friendships.FindAsync(id);
        if (f != null && f.Status == 0 && f.ActionUserId != myId && (f.User1Id == myId || f.User2Id == myId))
        {
            f.Status = 1;
            var streak = new KetBanChoiChuoi.Models.Streak { FriendshipId = f.Id };
            _db.Streaks.Add(streak);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Notifications");
    }

    [HttpPost]
    public async Task<IActionResult> Decline(int id)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var f = await _db.Friendships.FindAsync(id);
        if (f != null && f.Status == 0 && f.ActionUserId != myId && (f.User1Id == myId || f.User2Id == myId))
        {
            _db.Friendships.Remove(f);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Notifications");
    }

    [HttpPost]
    public async Task<IActionResult> Unfriend(int targetUserId)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var f = await _db.Friendships.FirstOrDefaultAsync(f => ((f.User1Id == myId && f.User2Id == targetUserId) || (f.User1Id == targetUserId && f.User2Id == myId)) && f.Status == 1);
        if (f != null)
        {
            _db.Friendships.Remove(f);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Search");
    }
}
