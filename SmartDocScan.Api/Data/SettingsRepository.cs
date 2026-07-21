using Npgsql;
using Microsoft.AspNetCore.DataProtection;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class SettingsRepository
{
    private readonly string _connectionString;
    private readonly IDataProtector _secretProtector;
    private readonly bool _autoEnsureSchema;

    public SettingsRepository(IConfiguration configuration, IDataProtectionProvider dataProtectionProvider)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _secretProtector = dataProtectionProvider.CreateProtector("SmartDocScan.Settings.Secrets.v1");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    public async Task<SecuritySettingsDto> GetSecuritySettingsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, _autoEnsureSchema, cancellationToken);
        var values = await ReadSettingsAsync(connection, cancellationToken);

        return new SecuritySettingsDto
        {
            Microsoft = new MicrosoftSsoSettingsDto
            {
                ClientId = Get(values, "Authentication:Microsoft:ClientId", configuration["Authentication:Microsoft:ClientId"]),
                ClientSecret = "",
                HasClientSecret = !string.IsNullOrWhiteSpace(GetSecret(values, "Authentication:Microsoft:ClientSecret", configuration["Authentication:Microsoft:ClientSecret"])),
                CallbackPath = Get(values, "Authentication:Microsoft:CallbackPath", configuration["Authentication:Microsoft:CallbackPath"] ?? "/api/auth/microsoft/callback")
            },
            Smtp = new SmtpSettingsDto
            {
                Host = Get(values, "Smtp:Host", configuration["Smtp:Host"]),
                Port = Get(values, "Smtp:Port", configuration["Smtp:Port"] ?? "587"),
                EnableSsl = Get(values, "Smtp:EnableSsl", configuration["Smtp:EnableSsl"] ?? "true"),
                From = Get(values, "Smtp:From", configuration["Smtp:From"] ?? "no-reply@ashunya.com"),
                Username = Get(values, "Smtp:Username", configuration["Smtp:Username"]),
                Password = "",
                HasPassword = !string.IsNullOrWhiteSpace(GetSecret(values, "Smtp:Password", configuration["Smtp:Password"]))
            },
            Branding = new BrandingSettingsDto
            {
                LogoDataUrl = Get(values, "Branding:LogoDataUrl", configuration["Branding:LogoDataUrl"])
            }
        };
    }

    public async Task<BrandingSettingsDto> GetBrandingSettingsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, _autoEnsureSchema, cancellationToken);
        var values = await ReadSettingsAsync(connection, cancellationToken);
        return new BrandingSettingsDto
        {
            LogoDataUrl = Get(values, "Branding:LogoDataUrl", configuration["Branding:LogoDataUrl"])
        };
    }

    public async Task<MicrosoftSsoSettingsDto> GetMicrosoftSsoRuntimeSettingsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, _autoEnsureSchema, cancellationToken);
        var values = await ReadSettingsAsync(connection, cancellationToken);

        return new MicrosoftSsoSettingsDto
        {
            ClientId = Get(values, "Authentication:Microsoft:ClientId", configuration["Authentication:Microsoft:ClientId"]),
            ClientSecret = GetSecret(values, "Authentication:Microsoft:ClientSecret", configuration["Authentication:Microsoft:ClientSecret"]),
            CallbackPath = Get(values, "Authentication:Microsoft:CallbackPath", configuration["Authentication:Microsoft:CallbackPath"] ?? "/api/auth/microsoft/callback"),
            HasClientSecret = !string.IsNullOrWhiteSpace(GetSecret(values, "Authentication:Microsoft:ClientSecret", configuration["Authentication:Microsoft:ClientSecret"]))
        };
    }

    public async Task<SmtpSettingsDto> GetSmtpRuntimeSettingsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, _autoEnsureSchema, cancellationToken);
        var values = await ReadSettingsAsync(connection, cancellationToken);

        return new SmtpSettingsDto
        {
            Host = Get(values, "Smtp:Host", configuration["Smtp:Host"]),
            Port = Get(values, "Smtp:Port", configuration["Smtp:Port"] ?? "587"),
            EnableSsl = Get(values, "Smtp:EnableSsl", configuration["Smtp:EnableSsl"] ?? "true"),
            From = Get(values, "Smtp:From", configuration["Smtp:From"] ?? "no-reply@ashunya.com"),
            Username = Get(values, "Smtp:Username", configuration["Smtp:Username"]),
            Password = GetSecret(values, "Smtp:Password", configuration["Smtp:Password"]),
            HasPassword = !string.IsNullOrWhiteSpace(GetSecret(values, "Smtp:Password", configuration["Smtp:Password"]))
        };
    }

    public async Task SaveSecuritySettingsAsync(SecuritySettingsDto settings, CancellationToken cancellationToken = default)
    {
        var microsoft = settings.Microsoft ?? new MicrosoftSsoSettingsDto();
        var smtp = settings.Smtp ?? new SmtpSettingsDto();
        var branding = settings.Branding ?? new BrandingSettingsDto();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, _autoEnsureSchema, cancellationToken);

        await UpsertAsync(connection, "Authentication:Microsoft:ClientId", microsoft.ClientId, cancellationToken);
        await UpsertSecretAsync(connection, "Authentication:Microsoft:ClientSecret", microsoft.ClientSecret, cancellationToken);
        await UpsertAsync(connection, "Authentication:Microsoft:CallbackPath", microsoft.CallbackPath, cancellationToken);
        await UpsertAsync(connection, "Smtp:Host", smtp.Host, cancellationToken);
        await UpsertAsync(connection, "Smtp:Port", smtp.Port, cancellationToken);
        await UpsertAsync(connection, "Smtp:EnableSsl", smtp.EnableSsl, cancellationToken);
        await UpsertAsync(connection, "Smtp:From", smtp.From, cancellationToken);
        await UpsertAsync(connection, "Smtp:Username", smtp.Username, cancellationToken);
        await UpsertSecretAsync(connection, "Smtp:Password", smtp.Password, cancellationToken);
        await UpsertAsync(connection, "Branding:LogoDataUrl", branding.LogoDataUrl, cancellationToken);
    }

    public static void LoadIntoConfiguration(IConfigurationManager configuration)
    {
        var connectionString = configuration.GetConnectionString("SmartDocScan");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            if (DatabaseSchemaOptions.AutoEnsureSchema(configuration))
            {
                using var ensure = connection.CreateCommand();
                ensure.CommandText = EnsureTableSql;
                ensure.ExecuteNonQuery();
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT setting_key, setting_value FROM app_setting;";
            using var reader = command.ExecuteReader();
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                values[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
            configuration.AddInMemoryCollection(values);
        }
        catch
        {
            // The app can still start with appsettings/.env values if the DB is unavailable.
        }
    }

    private static async Task<Dictionary<string, string?>> ReadSettingsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT setting_key, setting_value FROM app_setting;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
        return values;
    }

    private static async Task EnsureTableAsync(NpgsqlConnection connection, bool autoEnsureSchema, CancellationToken cancellationToken)
    {
        if (!autoEnsureSchema)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = EnsureTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpsertSecretAsync(NpgsqlConnection connection, string key, string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        await UpsertAsync(connection, key, ProtectSecret(value.Trim()), cancellationToken);
    }

    private static async Task UpsertAsync(NpgsqlConnection connection, string key, string? value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_setting (setting_key, setting_value)
            VALUES (@key, @value)
            ON CONFLICT (setting_key)
            DO UPDATE SET setting_value = EXCLUDED.setting_value,
                          updated_on = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC');
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> values, string key, string? fallback)
    {
        return values.TryGetValue(key, out var value) ? value : fallback;
    }

    private string? GetSecret(IReadOnlyDictionary<string, string?> values, string key, string? fallback)
    {
        return UnprotectSecret(Get(values, key, fallback));
    }

    private string ProtectSecret(string value)
    {
        return ProtectedSecretPrefix + _secretProtector.Protect(value);
    }

    private string? UnprotectSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!value.StartsWith(ProtectedSecretPrefix, StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            return _secretProtector.Unprotect(value[ProtectedSecretPrefix.Length..]);
        }
        catch
        {
            return null;
        }
    }

    private const string EnsureTableSql = """
        CREATE TABLE IF NOT EXISTS app_setting (
            setting_key varchar(160) PRIMARY KEY,
            setting_value text NULL,
            updated_on timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
        );
        """;
    private const string ProtectedSecretPrefix = "protected:";
}
