using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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
        
        if (string.IsNullOrEmpty(smtpEmail) || smtpEmail == "YOUR_GMAIL_HERE" || string.IsNullOrEmpty(smtpPassword))
        {
            // Fallback for testing if email isn't configured properly
            System.Console.WriteLine($"[EMAIL SIMULATION] To: {toEmail} | Subject: {subject} | Body: {body}");
            return;
        }

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
    }
}
