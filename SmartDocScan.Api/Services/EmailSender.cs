using System.Net;
using System.Net.Mail;
using SmartDocScan.Api.Data;

namespace SmartDocScan.Api.Services;

public interface IEmailSender
{
    Task SendLoginOtpAsync(string to, string code, CancellationToken cancellationToken = default);
}

public sealed class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly SettingsRepository _settingsRepository;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, SettingsRepository settingsRepository, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public async Task SendLoginOtpAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetSmtpRuntimeSettingsAsync(_configuration, cancellationToken);
        var host = settings.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("SmartDocScan login OTP for {Email}: {Code}", to, code);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.From ?? "no-reply@ashunya.com", "SmartDocScan"),
            Subject = "Your SmartDocScan verification code",
            Body = $"Your SmartDocScan verification code is {code}. This code expires in 10 minutes.",
            IsBodyHtml = false
        };
        message.To.Add(to);

        using var client = new SmtpClient(host, int.TryParse(settings.Port, out var port) ? port : 587)
        {
            EnableSsl = bool.TryParse(settings.EnableSsl, out var enableSsl) ? enableSsl : true
        };

        var username = settings.Username;
        var password = settings.Password;
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
