using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using KetBanChoiChuoi.Hubs;

namespace KetBanChoiChuoi.Controllers;

[Authorize]
public class FriendController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public FriendController(AppDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private int MyId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ================= SEARCH =================
    [HttpGet]
    public IActionResult Search() => View();

    [HttpGet]
    public async Task<IActionResult> SearchAjax(string q)
    {
        int myId = MyId();

        if (string.IsNullOrWhiteSpace(q))
            return Json(new List<object>());

        q = q.Trim();

        // lấy user trước
        var users = await _db.Users
            .Where(u => u.Id != myId &&
                        (u.DisplayName.Contains(q) || u.Username.Contains(q)))
            .OrderBy(u => u.DisplayName)
            .Take(5)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();

        // lấy friendship 1 lần
        var friendships = await _db.Friendships
            .Where(f =>
                (f.User1Id == myId && userIds.Contains(f.User2Id)) ||
                (f.User2Id == myId && userIds.Contains(f.User1Id)))
            .ToListAsync();

        var result = users.Select(u =>
        {
            var f = friendships.FirstOrDefault(x =>
                (x.User1Id == myId && x.User2Id == u.Id) ||
                (x.User2Id == myId && x.User1Id == u.Id));

            return new
            {
                id = u.Id,
                displayName = u.DisplayName,
                username = u.Username,
                avatarUrl = u.AvatarUrl,
                status = f != null ? f.Status : -1, // -1 = chưa kết bạn
                actionUserId = f != null ? f.ActionUserId : -1
            };
        });

        return Json(result);
    }

    // ================= PENDING COUNT =================
    [HttpGet]
    public async Task<IActionResult> GetPendingCount()
    {
        int myId = MyId();

        int count = await _db.Friendships.CountAsync(f =>
            (f.User1Id == myId || f.User2Id == myId) &&
            f.Status == 0 &&
            f.ActionUserId != myId);

        return Json(new { count });
    }

    // ================= SEND REQUEST =================
    [HttpPost]
    public async Task<IActionResult> SendRequest(int targetUserId)
    {
        int myId = MyId();

        if (myId == targetUserId)
            return RedirectToAction("Search");

        var exists = await _db.Friendships.AnyAsync(f =>
            (f.User1Id == myId && f.User2Id == targetUserId) ||
            (f.User1Id == targetUserId && f.User2Id == myId));

        if (!exists)
        {
            var f = new KetBanChoiChuoi.Models.Friendship
            {
                User1Id = myId,
                User2Id = targetUserId,
                Status = 0,
                ActionUserId = myId
            };

            _db.Friendships.Add(f);
            await _db.SaveChangesAsync();

            // realtime notify
            await _hub.Clients.User(targetUserId.ToString())
                .SendAsync("ReceiveNotification");
        }

        return RedirectToAction("Search");
    }

    // ================= CANCEL REQUEST =================
    [HttpPost]
    public async Task<IActionResult> CancelRequest(int targetUserId)
    {
        int myId = MyId();

        var f = await _db.Friendships.FirstOrDefaultAsync(f =>
            ((f.User1Id == myId && f.User2Id == targetUserId) ||
             (f.User1Id == targetUserId && f.User2Id == myId)) &&
            f.Status == 0 &&
            f.ActionUserId == myId);

        if (f != null)
        {
            _db.Friendships.Remove(f);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Search");
    }

    // ================= NOTIFICATIONS =================
    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        int myId = MyId();

        var pending = await _db.Friendships
            .Include(f => f.User1)
            .Include(f => f.User2)
            .Where(f =>
                (f.User1Id == myId || f.User2Id == myId) &&
                f.Status == 0 &&
                f.ActionUserId != myId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return View(pending);
    }

    // ================= ACCEPT =================
    [HttpPost]
    public async Task<IActionResult> Accept(int id)
    {
        int myId = MyId();

        var f = await _db.Friendships.FindAsync(id);

        if (f != null &&
            f.Status == 0 &&
            f.ActionUserId != myId &&
            (f.User1Id == myId || f.User2Id == myId))
        {
            f.Status = 1;

            var streak = new KetBanChoiChuoi.Models.Streak
            {
                FriendshipId = f.Id
            };

            _db.Streaks.Add(streak);

            await _db.SaveChangesAsync();

            // notify cả 2 user
            await _hub.Clients.Users(
                f.User1Id.ToString(),
                f.User2Id.ToString()
            ).SendAsync("FriendAccepted");
        }

        return RedirectToAction("Notifications");
    }

    // ================= DECLINE =================
    [HttpPost]
    public async Task<IActionResult> Decline(int id)
    {
        int myId = MyId();

        var f = await _db.Friendships.FindAsync(id);

        if (f != null &&
            f.Status == 0 &&
            f.ActionUserId != myId &&
            (f.User1Id == myId || f.User2Id == myId))
        {
            _db.Friendships.Remove(f);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Notifications");
    }

    // ================= UNFRIEND =================
    [HttpPost]
    public async Task<IActionResult> Unfriend(int targetUserId)
    {
        int myId = MyId();

        var f = await _db.Friendships.FirstOrDefaultAsync(f =>
            ((f.User1Id == myId && f.User2Id == targetUserId) ||
             (f.User1Id == targetUserId && f.User2Id == myId)) &&
            f.Status == 1);

        if (f != null)
        {
            var streak = await _db.Streaks
                .FirstOrDefaultAsync(s => s.FriendshipId == f.Id);

            if (streak != null)
                _db.Streaks.Remove(streak);

            _db.Friendships.Remove(f);

            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Search");
    }
}