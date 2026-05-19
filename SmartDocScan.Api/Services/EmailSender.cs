using System.Net;
using System.Net.Mail;

namespace SmartDocScan.Api.Services;

public interface IEmailSender
{
    Task SendLoginOtpAsync(string to, string code, CancellationToken cancellationToken = default);
}

public sealed class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendLoginOtpAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("SmartDocScan login OTP for {Email}: {Code}", to, code);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_configuration["Smtp:From"] ?? "no-reply@ashunya.com", "SmartDocScan"),
            Subject = "Your SmartDocScan verification code",
            Body = $"Your SmartDocScan verification code is {code}. This code expires in 10 minutes.",
            IsBodyHtml = false
        };
        message.To.Add(to);

        using var client = new SmtpClient(host, int.TryParse(_configuration["Smtp:Port"], out var port) ? port : 587)
        {
            EnableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var enableSsl) ? enableSsl : true
        };

        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
