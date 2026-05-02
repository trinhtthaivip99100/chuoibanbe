using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace KetBanChoiChuoi.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _db;
    private readonly Cloudinary _cloudinary;
    public ProfileController(AppDbContext db, Cloudinary cloudinary) 
    { 
        _db = db; 
        _cloudinary = cloudinary;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(myId);
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Update(string username, string displayName, IFormFile? avatarFile)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(myId);
        if (user == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(username) || username.Length < 5)
        {
            ViewBag.Error = "Mã ID phải từ 5 ký tự trở lên!";
            return View("Index", user);
        }
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 15)
        {
            ViewBag.Error = "Tên hiển thị không được vượt quá 15 ký tự!";
            return View("Index", user);
        }

        if (user.Username != username)
        {
            if (await _db.Users.AnyAsync(u => u.Username == username))
            {
                ViewBag.Error = "ID (Tên đăng nhập) đã có người sử dụng. Vui lòng chọn ID khác!";
                return View("Index", user);
            }
            user.Username = username;
        }

        user.DisplayName = displayName;

        if (avatarFile != null && avatarFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                int folderIndex = user.AvatarUrl.IndexOf("img/chuoibanbe/avatar/");
                if (folderIndex != -1)
                {
                    string publicIdWithExt = user.AvatarUrl.Substring(folderIndex);
                    int lastDotIndex = publicIdWithExt.LastIndexOf('.');
                    string publicId = lastDotIndex != -1 ? publicIdWithExt.Substring(0, lastDotIndex) : publicIdWithExt;
                    await _cloudinary.DestroyAsync(new DeletionParams(publicId) { Invalidate = true });
                }
            }

            using var stream = avatarFile.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(avatarFile.FileName, stream),
                Folder = "img/chuoibanbe/avatar"
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            user.AvatarUrl = uploadResult.SecureUrl.ToString();
        }

        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("DisplayName", user.DisplayName)
        };
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            claims.Add(new Claim("AvatarUrl", user.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        ViewBag.Success = "Cập nhật hồ sơ thành công!";
        return View("Index", user);
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        int myId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(myId);
        if (user == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            ViewBag.Error = "Mật khẩu mới phải từ 6 ký tự trở lên!";
            return View("Index", user);
        }

        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "Xác nhận mật khẩu không khớp!";
            return View("Index", user);
        }

        var hasher = new PasswordHasher<KetBanChoiChuoi.Models.User>();
        var result = hasher.VerifyHashedPassword(user, user.Password, oldPassword);

        if (result == PasswordVerificationResult.Failed)
        {
            ViewBag.Error = "Mật khẩu cũ không chính xác!";
            return View("Index", user);
        }

        user.Password = hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();

        ViewBag.Success = "Đổi mật khẩu thành công!";
        return View("Index", user);
    }
}
