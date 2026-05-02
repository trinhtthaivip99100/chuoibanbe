using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using KetBanChoiChuoi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace KetBanChoiChuoi.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    // ================= LOGIN =================
    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }

        if (user.IsLocked)
        {
            TempData["ErrorMessage"] = $"Tài khoản bị khóa: {user.LockReason}";
            return View();
        }

        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.Password, password);

        if (result == PasswordVerificationResult.Failed)
        {
            TempData["ErrorMessage"] = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }

        await SignInUser(user);

        TempData["SuccessMessage"] = "Đăng nhập thành công!";
        return RedirectToAction("Index", "Home");
    }

    // ================= REGISTER =================
    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(string username, string email, string password, string displayName)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 5)
        {
            TempData["ErrorMessage"] = "ID phải >= 5 ký tự!";
            return View();
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 15)
        {
            TempData["ErrorMessage"] = "Tên hiển thị tối đa 15 ký tự!";
            return View();
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            TempData["ErrorMessage"] = "Mật khẩu >= 6 ký tự!";
            return View();
        }

        if (await _db.Users.AnyAsync(u => u.Email == email || u.Username == username))
        {
            TempData["ErrorMessage"] = "Email hoặc ID đã tồn tại!";
            return View();
        }

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = displayName,
            IsEmailVerified = true  // bỏ qua xác thực email
        };

        var hasher = new PasswordHasher<User>();
        user.Password = hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInUser(user);

        TempData["SuccessMessage"] = "Đăng ký thành công!";
        return RedirectToAction("Index", "Home");
    }

    // ================= FORGOT PASSWORD =================
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Email không tồn tại!";
            return View();
        }

        // Tạm thời không gửi email được — thông báo liên hệ admin
        TempData["ErrorMessage"] = "Chức năng quên mật khẩu tạm thời không khả dụng. Vui lòng liên hệ admin!";
        return View();
    }

    // ================= LOGOUT =================
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ================= PRIVATE =================
    private async Task SignInUser(User user)
    {
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
    }
}