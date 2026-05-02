using Microsoft.EntityFrameworkCore;
using KetBanChoiChuoi.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using KetBanChoiChuoi.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddTransient<KetBanChoiChuoi.Services.EmailService>();

var cloudinaryAccount = new CloudinaryDotNet.Account(
    builder.Configuration["Cloudinary:CloudName"],
    builder.Configuration["Cloudinary:ApiKey"],
    builder.Configuration["Cloudinary:ApiSecret"]
);
var cloudinary = new CloudinaryDotNet.Cloudinary(cloudinaryAccount);
builder.Services.AddSingleton(cloudinary);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // db.Database.EnsureDeleted(); // Removed to persist data
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw(@"
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Pets' and xtype='U')
        BEGIN
            CREATE TABLE Pets (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(MAX) NOT NULL,
                RequiredStreak INT NOT NULL,
                ImageUrl NVARCHAR(MAX) NOT NULL
            )
        END
    ");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<NotificationHub>("/notificationHub");

app.Run();
