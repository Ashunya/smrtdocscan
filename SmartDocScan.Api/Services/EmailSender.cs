using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using SmartDocScan.Api.Data;
using SmartDocScan.Api.Models;
namespace SmartDocScan.Api.Services;

public interface IEmailSender
{
    Task SendLoginOtpAsync(string to, string code, CancellationToken cancellationToken = default);
    Task SendDocumentEmailAsync(string to, BusinessDocumentDto document, string documentUrl, string? tenantId, CancellationToken cancellationToken = default);
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

    public async Task SendDocumentEmailAsync(string to, BusinessDocumentDto document, string documentUrl, string? tenantId, CancellationToken cancellationToken = default)
    {
        var msSettings = await _settingsRepository.GetMicrosoftSsoRuntimeSettingsAsync(_configuration, cancellationToken);
        var hasMsSso = !string.IsNullOrWhiteSpace(msSettings.ClientId) && !string.IsNullOrWhiteSpace(msSettings.ClientSecret) && !string.IsNullOrWhiteSpace(tenantId);
        
        var subject = $"SmartDocScan: {document.DocumentName ?? "Business Document"}";
        var bodyText = $"Please review the following document:\n\n{documentUrl}";
        
        if (hasMsSso)
        {
            // Use Microsoft Graph API
            using var httpClient = new HttpClient();
            
            // 1. Get Token
            var tokenReq = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");
            tokenReq.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = msSettings.ClientId!,
                ["client_secret"] = msSettings.ClientSecret!,
                ["scope"] = "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials"
            });
            
            var tokenRes = await httpClient.SendAsync(tokenReq, cancellationToken);
            if (!tokenRes.IsSuccessStatusCode)
            {
                var error = await tokenRes.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get MS Graph token: {Error}", error);
                throw new Exception("Failed to authenticate with Microsoft Graph API.");
            }
            
            var tokenObj = await System.Text.Json.JsonSerializer.DeserializeAsync<System.Text.Json.Nodes.JsonObject>(
                await tokenRes.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var token = tokenObj?["access_token"]?.ToString();
            
            // 2. Send Mail (Graph API requires sending from a specific user if using Application permissions,
            // or if the app has Mail.Send, it can send as any user. We'll use the 'from' address in SMTP settings as sender if configured, 
            // or fallback to a hardcoded user, or use a default one. Actually, we can send as the sender's own email if we want, 
            // but we need an object ID or User Principal Name.)
            var smtpSettings = await _settingsRepository.GetSmtpRuntimeSettingsAsync(_configuration, cancellationToken);
            var senderEmail = !string.IsNullOrWhiteSpace(smtpSettings.From) ? smtpSettings.From : "no-reply@ashunya.com";
            
            var mailPayload = new
            {
                message = new
                {
                    subject = subject,
                    body = new { contentType = "Text", content = bodyText },
                    toRecipients = new[] { new { emailAddress = new { address = to } } }
                },
                saveToSentItems = false
            };
            
            var mailReq = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{senderEmail}/sendMail");
            mailReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            mailReq.Content = System.Net.Http.Json.JsonContent.Create(mailPayload);
            
            var mailRes = await httpClient.SendAsync(mailReq, cancellationToken);
            if (!mailRes.IsSuccessStatusCode)
            {
                var error = await mailRes.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send MS Graph email: {Error}", error);
                throw new Exception($"Failed to send email via Microsoft Graph. Check if {senderEmail} is a valid mailbox and app has Mail.Send permissions.");
            }
            return;
        }

        // Fallback to SMTP
        var settings = await _settingsRepository.GetSmtpRuntimeSettingsAsync(_configuration, cancellationToken);
        var host = settings.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new Exception("Email settings are not configured. Cannot send document.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.From ?? "no-reply@ashunya.com", "SmartDocScan"),
            Subject = subject,
            Body = bodyText,
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
