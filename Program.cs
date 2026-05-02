using KetBanChoiChuoi.Data;
using KetBanChoiChuoi.Hubs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC + SignalR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// DB PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Email Service
builder.Services.AddTransient<KetBanChoiChuoi.Services.EmailService>();

// Cloudinary
var cloudName = builder.Configuration["Cloudinary:CloudName"];
var apiKey = builder.Configuration["Cloudinary:ApiKey"];
var apiSecret = builder.Configuration["Cloudinary:ApiSecret"];

if (string.IsNullOrEmpty(cloudName) ||
    string.IsNullOrEmpty(apiKey) ||
    string.IsNullOrEmpty(apiSecret))
{
    throw new Exception("Cloudinary chưa cấu hình!");
}

var cloudinary = new CloudinaryDotNet.Cloudinary(
    new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret)
);

builder.Services.AddSingleton(cloudinary);

// Authentication (Cookie)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// ✅ Database migrate (QUAN TRỌNG)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR
app.MapHub<NotificationHub>("/notificationHub");

app.Run();