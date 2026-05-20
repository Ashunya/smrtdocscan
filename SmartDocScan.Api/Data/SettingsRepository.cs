using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class SettingsRepository
{
    private readonly string _connectionString;

    public SettingsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<SecuritySettingsDto> GetSecuritySettingsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);
        var values = await ReadSettingsAsync(connection, cancellationToken);

        return new SecuritySettingsDto
        {
            Microsoft = new MicrosoftSsoSettingsDto
            {
                ClientId = Get(values, "Authentication:Microsoft:ClientId", configuration["Authentication:Microsoft:ClientId"]),
                ClientSecret = "",
                HasClientSecret = !string.IsNullOrWhiteSpace(Get(values, "Authentication:Microsoft:ClientSecret", configuration["Authentication:Microsoft:ClientSecret"])),
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
                HasPassword = !string.IsNullOrWhiteSpace(Get(values, "Smtp:Password", configuration["Smtp:Password"]))
            },
            Branding = new BrandingSettingsDto
            {
                LogoDataUrl = Get(values, "Branding:LogoDataUrl", configuration["Branding:LogoDataUrl"])
            }
        };
    }

    public async Task<BrandingSettingsDto> GetBrandingSettingsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);
        var values = await ReadSettingsAsync(connection, cancellationToken);
        return new BrandingSettingsDto
        {
            LogoDataUrl = Get(values, "Branding:LogoDataUrl", configuration["Branding:LogoDataUrl"])
        };
    }

    public async Task<MicrosoftSsoSettingsDto> GetMicrosoftSsoRuntimeSettingsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);
        var values = await ReadSettingsAsync(connection, cancellationToken);

        return new MicrosoftSsoSettingsDto
        {
            ClientId = Get(values, "Authentication:Microsoft:ClientId", configuration["Authentication:Microsoft:ClientId"]),
            ClientSecret = Get(values, "Authentication:Microsoft:ClientSecret", configuration["Authentication:Microsoft:ClientSecret"]),
            CallbackPath = Get(values, "Authentication:Microsoft:CallbackPath", configuration["Authentication:Microsoft:CallbackPath"] ?? "/api/auth/microsoft/callback"),
            HasClientSecret = !string.IsNullOrWhiteSpace(Get(values, "Authentication:Microsoft:ClientSecret", configuration["Authentication:Microsoft:ClientSecret"]))
        };
    }

    public async Task SaveSecuritySettingsAsync(SecuritySettingsDto settings, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        await UpsertAsync(connection, "Authentication:Microsoft:ClientId", settings.Microsoft.ClientId, cancellationToken);
        await UpsertSecretAsync(connection, "Authentication:Microsoft:ClientSecret", settings.Microsoft.ClientSecret, cancellationToken);
        await UpsertAsync(connection, "Authentication:Microsoft:CallbackPath", settings.Microsoft.CallbackPath, cancellationToken);
        await UpsertAsync(connection, "Smtp:Host", settings.Smtp.Host, cancellationToken);
        await UpsertAsync(connection, "Smtp:Port", settings.Smtp.Port, cancellationToken);
        await UpsertAsync(connection, "Smtp:EnableSsl", settings.Smtp.EnableSsl, cancellationToken);
        await UpsertAsync(connection, "Smtp:From", settings.Smtp.From, cancellationToken);
        await UpsertAsync(connection, "Smtp:Username", settings.Smtp.Username, cancellationToken);
        await UpsertSecretAsync(connection, "Smtp:Password", settings.Smtp.Password, cancellationToken);
        await UpsertAsync(connection, "Branding:LogoDataUrl", settings.Branding.LogoDataUrl, cancellationToken);
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
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var ensure = connection.CreateCommand();
            ensure.CommandText = EnsureTableSql;
            ensure.ExecuteNonQuery();

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

    private static async Task<Dictionary<string, string?>> ReadSettingsAsync(SqlConnection connection, CancellationToken cancellationToken)
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

    private static async Task EnsureTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = EnsureTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertSecretAsync(SqlConnection connection, string key, string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        await UpsertAsync(connection, key, value, cancellationToken);
    }

    private static async Task UpsertAsync(SqlConnection connection, string key, string? value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            MERGE app_setting AS target
            USING (SELECT @key AS setting_key) AS source
              ON target.setting_key = source.setting_key
            WHEN MATCHED THEN
                UPDATE SET setting_value = @value, updated_on = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (setting_key, setting_value)
                VALUES (@key, @value);
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> values, string key, string? fallback)
    {
        return values.TryGetValue(key, out var value) ? value : fallback;
    }

    private const string EnsureTableSql = """
        IF OBJECT_ID('dbo.app_setting', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.app_setting (
                setting_key nvarchar(160) NOT NULL CONSTRAINT PK_app_setting PRIMARY KEY,
                setting_value nvarchar(max) NULL,
                updated_on datetime2 NOT NULL CONSTRAINT DF_app_setting_updated_on DEFAULT SYSUTCDATETIME()
            );
        END;
        """;
}
