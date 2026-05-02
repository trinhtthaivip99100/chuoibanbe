using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KetBanChoiChuoi.Data;
using KetBanChoiChuoi.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System;
using KetBanChoiChuoi.Services;
using System.Linq;

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

        if (!user.IsEmailVerified)
        {
            TempData["ErrorMessage"] = "Tài khoản chưa được xác thực Email!";
            return RedirectToAction("VerifyOtp", new { email = user.Email });
        }

        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.Password, password);

        if (result == PasswordVerificationResult.Failed)
        {
            TempData["ErrorMessage"] = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }

        if (user.IsLocked)
        {
            TempData["ErrorMessage"] = $"Tài khoản đã bị khóa! Lý do: {user.LockReason}";
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "User"),
            new Claim("DisplayName", user.DisplayName),
            new Claim("RoleId", user.RoleId.ToString())
        };
        
        if(!string.IsNullOrEmpty(user.AvatarUrl)) {
            claims.Add(new Claim("AvatarUrl", user.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });

        TempData["SuccessMessage"] = "Đăng nhập thành công!";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(string username, string email, string password, string displayName)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 5)
        {
            TempData["ErrorMessage"] = "Mã ID phải từ 5 ký tự trở lên!";
            return View();
        }
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 15)
        {
            TempData["ErrorMessage"] = "Tên hiển thị không được vượt quá 15 ký tự!";
            return View();
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            TempData["ErrorMessage"] = "Mật khẩu phải từ 6 ký tự trở lên!";
            return View();
        }

        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            TempData["ErrorMessage"] = "Tên tài khoản (Mã ID) đã tồn tại!";
            return View();
        }
        
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            TempData["ErrorMessage"] = "Email đã được sử dụng!";
            return View();
        }

        var user = new User { Username = username, Email = email, DisplayName = displayName, RoleId = 2, IsEmailVerified = false };
        var hasher = new PasswordHasher<User>();
        user.Password = hasher.HashPassword(user, password);

        _db.Users.Add(user);
        
        // Generate OTP
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();
        var otp = new OtpRecord {
            Email = email,
            Code = code,
            ExpiryTime = DateTime.UtcNow.AddMinutes(10),
            Purpose = "Register"
        };
        _db.OtpRecords.Add(otp);
        
        await _db.SaveChangesAsync();

        await _emailService.SendEmailAsync(email, "Mã xác thực Đăng ký - Chuỗi", $"Mã OTP của bạn là: <b>{code}</b>. Mã có hiệu lực trong 10 phút.");

        TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng kiểm tra Email để lấy mã OTP.";
        return RedirectToAction("VerifyOtp", new { email = email });
    }

    [HttpGet]
    public IActionResult VerifyOtp(string email)
    {
        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> VerifyOtp(string email, string code)
    {
        var otp = await _db.OtpRecords.FirstOrDefaultAsync(o => o.Email == email && o.Code == code && o.Purpose == "Register" && !o.IsUsed);
        
        if (otp == null || otp.ExpiryTime < DateTime.UtcNow)
        {
            TempData["ErrorMessage"] = "Mã OTP không hợp lệ hoặc đã hết hạn!";
            ViewBag.Email = email;
            return View();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            user.IsEmailVerified = true;
            otp.IsUsed = true;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xác thực Email thành công! Bạn có thể đăng nhập.";
            return RedirectToAction("Login");
        }

        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản với Email này!";
            return View();
        }

        // Tự động mark các OTP quên pass cũ của email này thành hết hạn (tuỳ chọn)
        var oldOtps = await _db.OtpRecords.Where(o => o.Email == email && o.Purpose == "ResetPassword" && !o.IsUsed).ToListAsync();
        foreach (var o in oldOtps) o.IsUsed = true;

        var random = new Random();
        var code = random.Next(100000, 999999).ToString();
        var otp = new OtpRecord {
            Email = email,
            Code = code,
            ExpiryTime = DateTime.UtcNow.AddMinutes(10),
            Purpose = "ResetPassword"
        };
        _db.OtpRecords.Add(otp);
        await _db.SaveChangesAsync();

        await _emailService.SendEmailAsync(email, "Mã khôi phục mật khẩu - Chuỗi", $"Mã OTP để khôi phục mật khẩu của bạn là: <b>{code}</b>. Mã có hiệu lực trong 10 phút.");

        TempData["SuccessMessage"] = "Mã xác nhận đã được gửi vào Email của bạn.";
        return RedirectToAction("ResetPassword", new { email = email });
    }

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
            TempData["ErrorMessage"] = "Mật khẩu phải từ 6 ký tự trở lên!";
            ViewBag.Email = email;
            return View();
        }

        var otp = await _db.OtpRecords.FirstOrDefaultAsync(o => o.Email == email && o.Code == code && o.Purpose == "ResetPassword" && !o.IsUsed);
        
        if (otp == null || otp.ExpiryTime < DateTime.UtcNow)
        {
            TempData["ErrorMessage"] = "Mã OTP không hợp lệ hoặc đã hết hạn!";
            ViewBag.Email = email;
            return View();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            var hasher = new PasswordHasher<User>();
            user.Password = hasher.HashPassword(user, newPassword);
            otp.IsUsed = true;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
            return RedirectToAction("Login");
        }

        return RedirectToAction("Login");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
