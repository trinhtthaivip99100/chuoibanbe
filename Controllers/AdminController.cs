using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KetBanChoiChuoi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using KetBanChoiChuoi.Hubs;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace KetBanChoiChuoi.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly Cloudinary _cloudinary;

    public AdminController(AppDbContext db, IHubContext<NotificationHub> hub, Cloudinary cloudinary)
    {
        _db = db;
        _hub = hub;
        _cloudinary = cloudinary;
    }

    // ================= DASHBOARD =================
    public async Task<IActionResult> Index()
    {
        ViewBag.TotalUsers = await _db.Users.CountAsync();
        ViewBag.ActiveUsers = await _db.Users.CountAsync(u => !u.IsLocked);
        ViewBag.TotalConnections = await _db.Friendships.CountAsync(f => f.Status == 1);

        var users = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var friendships = await _db.Friendships
            .Include(f => f.User1)
            .Include(f => f.User2)
            .Where(f => f.Status == 1)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        var streaks = await _db.Streaks.ToListAsync();

        var pets = await _db.Pets
            .OrderBy(p => p.RequiredStreak)
            .ToListAsync();

        ViewBag.Friendships = friendships;
        ViewBag.Streaks = streaks;
        ViewBag.Pets = pets;

        return View(users);
    }

    // ================= LOCK USER =================
    [HttpPost]
    public async Task<IActionResult> LockUser(int id, string? reason)
    {
        var user = await _db.Users.FindAsync(id);

        if (user != null && user.RoleId != 1)
        {
            user.IsLocked = true;
            user.LockReason = string.IsNullOrWhiteSpace(reason)
                ? "Vi phạm nội quy"
                : reason;

            await _db.SaveChangesAsync();

            // force logout realtime
            await _hub.Clients.User(id.ToString()).SendAsync("ForceLogout");

            TempData["Success"] = $"Đã khóa {user.Username}";
        }

        return RedirectToAction("Index");
    }

    // ================= UNLOCK USER =================
    [HttpPost]
    public async Task<IActionResult> UnlockUser(int id)
    {
        var user = await _db.Users.FindAsync(id);

        if (user != null)
        {
            user.IsLocked = false;
            user.LockReason = null;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Đã mở khóa {user.Username}";
        }

        return RedirectToAction("Index");
    }

    // ================= UPDATE STREAK =================
    [HttpPost]
    public async Task<IActionResult> UpdateStreak(int friendshipId, int count)
    {
        var streak = await _db.Streaks
            .FirstOrDefaultAsync(s => s.FriendshipId == friendshipId);

        if (streak != null)
        {
            streak.CurrentStreakCount = Math.Max(0, count);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật streak!";
        }

        return RedirectToAction("Index");
    }

    // ================= ADD PET =================
    [HttpPost]
    public async Task<IActionResult> AddPet(string name, int requiredStreak, IFormFile image)
    {
        if (string.IsNullOrWhiteSpace(name) || image == null)
        {
            TempData["Error"] = "Thiếu dữ liệu!";
            return RedirectToAction("Index");
        }

        // check trùng
        bool exists = await _db.Pets.AnyAsync(p =>
            p.Name.ToLower() == name.ToLower() ||
            p.RequiredStreak == requiredStreak);

        if (exists)
        {
            TempData["Error"] = "Tên hoặc mốc streak đã tồn tại!";
            return RedirectToAction("Index");
        }

        // validate file
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(image.ContentType))
        {
            TempData["Error"] = "Ảnh không hợp lệ!";
            return RedirectToAction("Index");
        }

        // tăng lên 5MB vì GIF thường nặng hơn
        long maxSize = 10 * 1024 * 1024; // 10MB

        if (image.Length > maxSize)
        {
            TempData["Error"] = $"Ảnh tối đa {maxSize / 1024 / 1024}MB!";
            return RedirectToAction("Index");
        }

        // upload cloudinary
        using var stream = image.OpenReadStream();

        var upload = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(image.FileName, stream),
            Folder = "img/chuoibanbe/pets",
            Transformation = new Transformation()
                .Width(300).Height(300).Crop("fill")
        });

        var pet = new KetBanChoiChuoi.Models.Pet
        {
            Name = name,
            RequiredStreak = requiredStreak,
            ImageUrl = upload.SecureUrl.ToString()
        };

        _db.Pets.Add(pet);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm pet {name}";
        return RedirectToAction("Index");
    }

    // ================= DELETE PET =================
    [HttpPost]
    public async Task<IActionResult> DeletePet(int id)
    {
        var pet = await _db.Pets.FindAsync(id);

        if (pet != null)
        {
            try
            {
                int index = pet.ImageUrl.IndexOf("img/chuoibanbe/pets/");
                if (index != -1)
                {
                    string publicId = pet.ImageUrl.Substring(index);
                    int dot = publicId.LastIndexOf('.');
                    if (dot != -1) publicId = publicId.Substring(0, dot);

                    await _cloudinary.DestroyAsync(new DeletionParams(publicId)
                    {
                        Invalidate = true
                    });
                }
            }
            catch { }

            _db.Pets.Remove(pet);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã xóa pet!";
        }

        return RedirectToAction("Index");
    }

    // ================= EDIT PET =================
    [HttpPost]
    public async Task<IActionResult> EditPet(int id, string name, int requiredStreak, IFormFile? image)
    {
        var pet = await _db.Pets.FindAsync(id);
        if (pet == null) return RedirectToAction("Index");

        bool exists = await _db.Pets.AnyAsync(p =>
            p.Id != id &&
            (p.Name.ToLower() == name.ToLower() ||
             p.RequiredStreak == requiredStreak));

        if (exists)
        {
            TempData["Error"] = "Trùng tên hoặc mốc streak!";
            return RedirectToAction("Index");
        }

        pet.Name = name;
        pet.RequiredStreak = requiredStreak;

        if (image != null && image.Length > 0)
        {
            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowed.Contains(image.ContentType))
            {
                TempData["Error"] = "Ảnh không hợp lệ!";
                return RedirectToAction("Index");
            }

            // xóa ảnh cũ
            try
            {
                int index = pet.ImageUrl.IndexOf("img/chuoibanbe/pets/");
                if (index != -1)
                {
                    string publicId = pet.ImageUrl.Substring(index);
                    int dot = publicId.LastIndexOf('.');
                    if (dot != -1) publicId = publicId.Substring(0, dot);

                    await _cloudinary.DestroyAsync(new DeletionParams(publicId));
                }
            }
            catch { }

            using var stream = image.OpenReadStream();

            var upload = await _cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(image.FileName, stream),
                Folder = "img/chuoibanbe/pets"
            });

            pet.ImageUrl = upload.SecureUrl.ToString();
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Cập nhật pet thành công!";
        return RedirectToAction("Index");
    }
}