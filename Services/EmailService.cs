using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;


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

        Console.WriteLine($"[SMTP DEBUG] Bắt đầu gửi tới {toEmail}");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Admin Chuỗi", smtpEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            Console.WriteLine("[SMTP] Connected!");

            await client.AuthenticateAsync(smtpEmail, smtpPassword);
            Console.WriteLine("[SMTP] Authenticated!");

            await client.SendAsync(message);
            Console.WriteLine($"[SMTP SUCCESS] Đã gửi tới {toEmail}");

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTP ERROR] {ex.Message}");
            Console.WriteLine($"[SMTP INNER] {ex.InnerException?.Message}");
            throw;
        }
    }
}