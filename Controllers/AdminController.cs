using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KetBanChoiChuoi.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using KetBanChoiChuoi.Hubs;

namespace KetBanChoiChuoi.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;

    public AdminController(AppDbContext db, IHubContext<NotificationHub> hubContext, CloudinaryDotNet.Cloudinary cloudinary)
    {
        _db = db;
        _hubContext = hubContext;
        _cloudinary = cloudinary;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalUsers = await _db.Users.CountAsync();
        ViewBag.ActiveUsers = await _db.Users.CountAsync(u => !u.IsLocked);
        ViewBag.TotalConnections = await _db.Friendships.CountAsync(f => f.Status == 1);

        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var friendships = await _db.Friendships
            .Include(f => f.User1)
            .Include(f => f.User2)
            .Where(f => f.Status == 1)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        ViewBag.Friendships = friendships;
        ViewBag.Streaks = await _db.Streaks.ToListAsync();
        ViewBag.Pets = await _db.Pets.OrderBy(p => p.RequiredStreak).ToListAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> LockUser(int id, string reason)
    {
        var user = await _db.Users.FindAsync(id);
        if (user != null && user.RoleId != 1) // Ngăn khóa Admin khác
        {
            user.IsLocked = true;
            user.LockReason = string.IsNullOrWhiteSpace(reason) ? "Vi phạm nội quy" : reason;
            await _db.SaveChangesAsync();
            await _hubContext.Clients.User(id.ToString()).SendAsync("ForceLogout");
            TempData["SuccessMessage"] = $"Đã khóa tài khoản {user.Username}!";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> UnlockUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            user.IsLocked = false;
            user.LockReason = null;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã mở khóa tài khoản {user.Username}!";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStreak(int friendshipId, int currentStreakCount)
    {
        var streak = await _db.Streaks.FirstOrDefaultAsync(s => s.FriendshipId == friendshipId);
        if (streak != null)
        {
            streak.CurrentStreakCount = currentStreakCount;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật số ngày chuỗi thành {currentStreakCount}!";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> AddPet(string name, int requiredStreak, Microsoft.AspNetCore.Http.IFormFile image)
    {
        if(string.IsNullOrEmpty(name) || image == null) return RedirectToAction("Index");

        bool exists = await _db.Pets.AnyAsync(p => p.Name.ToLower() == name.ToLower() || p.RequiredStreak == requiredStreak);
        if (exists)
        {
            TempData["ErrorMessage"] = "Tên thú cưng hoặc Mốc chuỗi này đã tồn tại! Vui lòng chọn giá trị khác.";
            return RedirectToAction("Index");
        }

        var uploadResult = new CloudinaryDotNet.Actions.ImageUploadResult();
        using (var stream = image.OpenReadStream())
        {
            var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams()
            {
                File = new CloudinaryDotNet.FileDescription(image.FileName, stream),
                Folder = "img/chuoibanbe/pets"
            };
            uploadResult = await _cloudinary.UploadAsync(uploadParams);
        }
        
        var pet = new Models.Pet {
            Name = name,
            RequiredStreak = requiredStreak,
            ImageUrl = uploadResult.SecureUrl.ToString()
        };
        _db.Pets.Add(pet);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã thêm thú cưng: {name}!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeletePet(int id)
    {
        var pet = await _db.Pets.FindAsync(id);
        if(pet != null)
        {
            int folderIndex = pet.ImageUrl.IndexOf("img/chuoibanbe/pets/");
            if (folderIndex != -1)
            {
                string publicIdWithExt = pet.ImageUrl.Substring(folderIndex);
                int lastDotIndex = publicIdWithExt.LastIndexOf('.');
                string publicId = lastDotIndex != -1 ? publicIdWithExt.Substring(0, lastDotIndex) : publicIdWithExt;
                await _cloudinary.DestroyAsync(new CloudinaryDotNet.Actions.DeletionParams(publicId) { Invalidate = true });
            }
            _db.Pets.Remove(pet);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa thú cưng thành công!";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> EditPet(int id, string name, int requiredStreak, Microsoft.AspNetCore.Http.IFormFile? image)
    {
        var pet = await _db.Pets.FindAsync(id);
        if (pet == null) return RedirectToAction("Index");

        bool exists = await _db.Pets.AnyAsync(p => p.Id != id && (p.Name.ToLower() == name.ToLower() || p.RequiredStreak == requiredStreak));
        if (exists)
        {
            TempData["ErrorMessage"] = "Tên thú cưng hoặc Mốc chuỗi này đã bị trùng với thú cưng khác! Vui lòng chọn giá trị khác.";
            return RedirectToAction("Index");
        }

        if (image != null)
        {
            int folderIndex = pet.ImageUrl.IndexOf("img/chuoibanbe/pets/");
            if (folderIndex != -1)
            {
                string publicIdWithExt = pet.ImageUrl.Substring(folderIndex);
                int lastDotIndex = publicIdWithExt.LastIndexOf('.');
                string publicId = lastDotIndex != -1 ? publicIdWithExt.Substring(0, lastDotIndex) : publicIdWithExt;
                await _cloudinary.DestroyAsync(new CloudinaryDotNet.Actions.DeletionParams(publicId) { Invalidate = true });
            }

            using (var stream = image.OpenReadStream())
            {
                var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams()
                {
                    File = new CloudinaryDotNet.FileDescription(image.FileName, stream),
                    Folder = "img/chuoibanbe/pets"
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                pet.ImageUrl = uploadResult.SecureUrl.ToString();
            }
        }

        pet.Name = name;
        pet.RequiredStreak = requiredStreak;
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật thú cưng: {name}!";
        return RedirectToAction("Index");
    }
}
