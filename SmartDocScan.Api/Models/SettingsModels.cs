namespace SmartDocScan.Api.Models;

public sealed class SecuritySettingsDto
{
    public MicrosoftSsoSettingsDto Microsoft { get; set; } = new();
    public SmtpSettingsDto Smtp { get; set; } = new();
    public BrandingSettingsDto Branding { get; set; } = new();
}

public sealed class MicrosoftSsoSettingsDto
{
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool HasClientSecret { get; set; }
    public string? CallbackPath { get; set; }
}

public sealed class SmtpSettingsDto
{
    public string? Host { get; set; }
    public string? Port { get; set; }
    public string? EnableSsl { get; set; }
    public string? From { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool HasPassword { get; set; }
}

public sealed class BrandingSettingsDto
{
    public string? LogoDataUrl { get; set; }
}
