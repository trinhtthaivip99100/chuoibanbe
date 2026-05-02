using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace KetBanChoiChuoi.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var smtpEmail = _config["Smtp:Email"];
        var smtpPassword = _config["Smtp:Password"];

        Console.WriteLine($"[SMTP DEBUG] Email={smtpEmail}, HasPassword={!string.IsNullOrEmpty(smtpPassword)}");

        try
        {
            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpEmail, smtpPassword)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpEmail, "Admin Chuỗi"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);
            await client.SendMailAsync(mailMessage);

            Console.WriteLine($"[SMTP SUCCESS] Đã gửi email tới {toEmail}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTP ERROR] {ex.Message}");
            Console.WriteLine($"[SMTP ERROR DETAIL] {ex.InnerException?.Message}");
            throw;
        }
    }
}
