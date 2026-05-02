using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using KetBanChoiChuoi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using KetBanChoiChuoi.Services;

namespace KetBanChoiChuoi.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailService _emailService;

    public AuthController(AppDbContext db, EmailService emailService)
    {
        _db = db;
        _emailService = emailService;
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

        if (!user.IsEmailVerified)
        {
            return RedirectToAction("VerifyOtp", new { email });
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
            DisplayName = displayName
        };

        var hasher = new PasswordHasher<User>();
        user.Password = hasher.HashPassword(user, password);

        _db.Users.Add(user);

        // OTP
        var code = new Random().Next(100000, 999999).ToString();

        _db.OtpRecords.Add(new OtpRecord
        {
            Email = email,
            Code = code,
            ExpiryTime = DateTimeOffset.UtcNow.AddMinutes(10),
            Purpose = "Register"
        });

        await _db.SaveChangesAsync();

        await _emailService.SendEmailAsync(
     email,
     "Mã OTP đăng ký",
     $"Mã OTP của bạn là: <b>{code}</b>. Có hiệu lực 10 phút."
 );

        TempData["SuccessMessage"] = "Đăng ký thành công! Kiểm tra email để lấy OTP.";
        return RedirectToAction("VerifyOtp", new { email });
    }

    // ================= VERIFY OTP =================
    [HttpGet]
    public IActionResult VerifyOtp(string email)
    {
        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> VerifyOtp(string email, string code)
    {
        var otp = await _db.OtpRecords
            .Where(o => o.Email == email && o.Code == code && !o.IsUsed && o.Purpose == "Register")
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        if (otp == null || otp.ExpiryTime < DateTimeOffset.UtcNow)
        {
            TempData["ErrorMessage"] = "OTP không hợp lệ hoặc hết hạn!";
            ViewBag.Email = email;
            return View();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return RedirectToAction("Login");

        user.IsEmailVerified = true;
        otp.IsUsed = true;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Xác thực thành công!";
        return RedirectToAction("Login");
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

        // chống spam OTP (60s)
        var lastOtp = await _db.OtpRecords
            .Where(o => o.Email == email && o.Purpose == "ResetPassword")
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        if (lastOtp != null && lastOtp.ExpiryTime > DateTimeOffset.UtcNow.AddMinutes(-9))
        {
            TempData["ErrorMessage"] = "Vui lòng đợi trước khi gửi OTP mới!";
            return View();
        }

        var code = new Random().Next(100000, 999999).ToString();

        _db.OtpRecords.Add(new OtpRecord
        {
            Email = email,
            Code = code,
            ExpiryTime = DateTimeOffset.UtcNow.AddMinutes(10),
            Purpose = "ResetPassword"
        });

        await _db.SaveChangesAsync();

        await _emailService.SendEmailAsync(
     email,
     "Mã OTP đăng ký",
     $"Mã OTP của bạn là: <b>{code}</b>. Có hiệu lực 10 phút."
 );

        TempData["SuccessMessage"] = "OTP đã gửi!";
        return RedirectToAction("ResetPassword", new { email });
    }

    // ================= RESET PASSWORD =================
    [HttpGet]
    public IActionResult ResetPassword(string email)
    {
        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string email, string code, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "Mật khẩu >= 6 ký tự!";
            ViewBag.Email = email;
            return View();
        }

        var otp = await _db.OtpRecords
            .FirstOrDefaultAsync(o => o.Email == email && o.Code == code && !o.IsUsed && o.Purpose == "ResetPassword");

        if (otp == null || otp.ExpiryTime < DateTimeOffset.UtcNow)
        {
            TempData["ErrorMessage"] = "OTP không hợp lệ!";
            ViewBag.Email = email;
            return View();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return RedirectToAction("Login");

        var hasher = new PasswordHasher<User>();
        user.Password = hasher.HashPassword(user, newPassword);

        otp.IsUsed = true;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("Login");
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