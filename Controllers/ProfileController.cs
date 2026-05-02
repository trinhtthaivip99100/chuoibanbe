using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using Microsoft.EntityFrameworkCore;
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

    private int MyId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ================= VIEW PROFILE =================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _db.Users.FindAsync(MyId());
        return View(user);
    }

    // ================= UPDATE PROFILE =================
    [HttpPost]
    public async Task<IActionResult> Update(string username, string displayName, IFormFile? avatarFile)
    {
        var user = await _db.Users.FindAsync(MyId());
        if (user == null) return RedirectToAction("Index", "Home");

        // validate
        if (string.IsNullOrWhiteSpace(username) || username.Length < 5)
        {
            TempData["Error"] = "ID phải >= 5 ký tự!";
            return RedirectToAction("Index");
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 15)
        {
            TempData["Error"] = "Tên hiển thị tối đa 15 ký tự!";
            return RedirectToAction("Index");
        }

        // check username trùng
        if (user.Username != username)
        {
            bool exists = await _db.Users.AnyAsync(u => u.Username == username);
            if (exists)
            {
                TempData["Error"] = "ID đã tồn tại!";
                return RedirectToAction("Index");
            }
            user.Username = username;
        }

        user.DisplayName = displayName;

        // ================= AVATAR =================
        if (avatarFile != null && avatarFile.Length > 0)
        {
            // check type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(avatarFile.ContentType))
            {
                TempData["Error"] = "Chỉ cho phép JPG, PNG, WEBP, GIF!";
                return RedirectToAction("Index");
            }

            // check size (5MB vì GIF thường nặng hơn)
            if (avatarFile.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "Ảnh tối đa 5MB!";
                return RedirectToAction("Index");
            }

            // xóa ảnh cũ (nếu có)
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                try
                {
                    int index = user.AvatarUrl.IndexOf("img/chuoibanbe/avatar/");
                    if (index != -1)
                    {
                        string publicId = user.AvatarUrl.Substring(index);
                        int dot = publicId.LastIndexOf('.');
                        if (dot != -1) publicId = publicId.Substring(0, dot);

                        await _cloudinary.DestroyAsync(new DeletionParams(publicId)
                        {
                            Invalidate = true
                        });
                    }
                }
                catch { /* bỏ qua lỗi xóa ảnh */ }
            }

            // upload mới
            using var stream = avatarFile.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(avatarFile.FileName, stream),
                Folder = "img/chuoibanbe/avatar",
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            user.AvatarUrl = result.SecureUrl.ToString();
        }

        await _db.SaveChangesAsync();

        // ================= REFRESH CLAIM =================
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User"),
            new Claim("DisplayName", user.DisplayName),
            new Claim("RoleId", user.RoleId.ToString())
        };

        if (!string.IsNullOrEmpty(user.AvatarUrl))
            claims.Add(new Claim("AvatarUrl", user.AvatarUrl));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignOutAsync();

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        TempData["Success"] = "Cập nhật thành công!";
        return RedirectToAction("Index");
    }

    // ================= CHANGE PASSWORD =================
    [HttpPost]
    public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        var user = await _db.Users.FindAsync(MyId());
        if (user == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "Mật khẩu >= 6 ký tự!";
            return RedirectToAction("Index");
        }

        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "Mật khẩu không khớp!";
            return RedirectToAction("Index");
        }

        var hasher = new PasswordHasher<KetBanChoiChuoi.Models.User>();
        var result = hasher.VerifyHashedPassword(user, user.Password, oldPassword);

        if (result == PasswordVerificationResult.Failed)
        {
            TempData["Error"] = "Mật khẩu cũ sai!";
            return RedirectToAction("Index");
        }

        user.Password = hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();

        // logout sau khi đổi pass
        await HttpContext.SignOutAsync();

        TempData["Success"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";
        return RedirectToAction("Login", "Auth");
    }
}