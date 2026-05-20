using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;
using SmartDocScan.Api.Services;

namespace SmartDocScan.Api.Data;

public sealed class AuthRepository
{
    private readonly string _connectionString;
    private readonly IEmailSender _emailSender;

    public AuthRepository(IConfiguration configuration, IEmailSender emailSender)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _emailSender = emailSender;
    }

    public async Task<bool> IsTenantAllowedAsync(int companyId, string provider, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM company_identity_tenant
            WHERE comp_id = @companyId
              AND provider = @provider
              AND tenant_id = @tenantId
              AND enabled = 1;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<UserDto?> FindExternalUserAsync(string provider, string tenantId, string subjectId, string? email, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var linkedUser = await FindLinkedExternalUserAsync(connection, provider, tenantId, subjectId, cancellationToken);
        if (linkedUser is not null)
        {
            return linkedUser;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var user = await FindUserByUsernameAsync(connection, email.Trim(), cancellationToken);
        if (user is null || user.Disabled)
        {
            return null;
        }

        if (!user.IsAdmin && !user.SuperUser && !await IsTenantAllowedAsync(user.CompanyId, provider, tenantId, cancellationToken))
        {
            return null;
        }

        await LinkExternalLoginAsync(connection, user.Username!, provider, tenantId, subjectId, email, cancellationToken);
        return user;
    }

    public async Task<Guid> CreateEmailOtpChallengeAsync(UserDto user, CancellationToken cancellationToken = default)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var challengeId = Guid.NewGuid();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureOtpTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO auth_otp_challenge (challenge_id, username, code_hash, purpose, expires_on)
            VALUES (@challengeId, @username, @codeHash, 'login', DATEADD(minute, 10, SYSUTCDATETIME()));
            """;
        command.Parameters.AddWithValue("@challengeId", challengeId);
        command.Parameters.AddWithValue("@username", user.Username!);
        command.Parameters.AddWithValue("@codeHash", HashCode(code));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await _emailSender.SendLoginOtpAsync(user.Username!, code, cancellationToken);
        return challengeId;
    }

    public async Task<UserDto?> VerifyEmailOtpChallengeAsync(Guid challengeId, string? code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)transaction;
        command.CommandText = """
            SELECT username, code_hash
            FROM auth_otp_challenge
            WHERE challenge_id = @challengeId
              AND purpose = 'login'
              AND consumed_on IS NULL
              AND expires_on > SYSUTCDATETIME();
            """;
        command.Parameters.AddWithValue("@challengeId", challengeId);

        string? username = null;
        string? codeHash = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                username = reader.GetString(0);
                codeHash = reader.GetString(1);
            }
        }

        if (username is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(codeHash!),
                Encoding.UTF8.GetBytes(HashCode(code.Trim()))))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await using var update = connection.CreateCommand();
        update.Transaction = (SqlTransaction)transaction;
        update.CommandText = "UPDATE auth_otp_challenge SET consumed_on = SYSUTCDATETIME() WHERE challenge_id = @challengeId;";
        update.Parameters.AddWithValue("@challengeId", challengeId);
        await update.ExecuteNonQueryAsync(cancellationToken);

        var user = await FindUserByUsernameAsync(connection, username, cancellationToken, (SqlTransaction)transaction);
        await transaction.CommitAsync(cancellationToken);
        return user?.Disabled == true ? null : user;
    }

    private static async Task EnsureOtpTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID('dbo.auth_otp_challenge', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.auth_otp_challenge (
                    challenge_id uniqueidentifier NOT NULL CONSTRAINT PK_auth_otp_challenge PRIMARY KEY,
                    username varchar(50) NOT NULL,
                    code_hash nvarchar(255) NOT NULL,
                    purpose varchar(30) NOT NULL,
                    expires_on datetime2 NOT NULL,
                    consumed_on datetime2 NULL,
                    created_on datetime2 NOT NULL CONSTRAINT DF_auth_otp_challenge_created_on DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX IX_auth_otp_challenge_username
                    ON dbo.auth_otp_challenge(username, purpose, expires_on);
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<UserDto?> FindLinkedExternalUserAsync(SqlConnection connection, string provider, string tenantId, string subjectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.username, u.name, u.comp_id, u.upload_doc, u.scan_doc, u.delete_doc, u.delete_manage,
                   u.print_doc, u.download_doc, u.add_cat, u.add_users, u.add_patients, u.box, u.report,
                   u.su, u.disabled, u.IsAdmin
            FROM usersinfo u
            INNER JOIN user_external_login x ON x.username = u.username
            WHERE x.provider = @provider
              AND x.tenant_id = @tenantId
              AND x.subject_id = @subjectId
              AND u.disabled = 0;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@subjectId", subjectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    private static async Task<UserDto?> FindUserByUsernameAsync(SqlConnection connection, string username, CancellationToken cancellationToken, SqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UserSelectSql + " WHERE u.username = @username;";
        command.Parameters.AddWithValue("@username", username);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    private static async Task LinkExternalLoginAsync(SqlConnection connection, string username, string provider, string tenantId, string subjectId, string? email, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF NOT EXISTS (
                SELECT 1 FROM user_external_login
                WHERE provider = @provider AND tenant_id = @tenantId AND subject_id = @subjectId
            )
            BEGIN
                INSERT INTO user_external_login (username, provider, tenant_id, subject_id, email)
                VALUES (@username, @provider, @tenantId, @subjectId, @email);
            END
            """;
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@subjectId", subjectId);
        command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string HashCode(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static UserDto MapUser(SqlDataReader reader)
    {
        return new UserDto
        {
            Username = ReadString(reader, "username"),
            Name = ReadString(reader, "name"),
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            UploadDocument = ReadByteFlag(reader, "upload_doc"),
            ScanDocument = ReadByteFlag(reader, "scan_doc"),
            DeleteDocument = ReadByteFlag(reader, "delete_doc"),
            DeleteManage = ReadByteFlag(reader, "delete_manage"),
            PrintDocument = ReadByteFlag(reader, "print_doc"),
            DownloadDocument = ReadByteFlag(reader, "download_doc"),
            AddCategory = ReadByteFlag(reader, "add_cat"),
            AddUsers = ReadByteFlag(reader, "add_users"),
            AddPatients = ReadByteFlag(reader, "add_patients"),
            Box = ReadByteFlag(reader, "box"),
            Report = ReadByteFlag(reader, "report"),
            SuperUser = ReadByteFlag(reader, "su"),
            Disabled = ReadByteFlag(reader, "disabled"),
            IsAdmin = ReadBool(reader, "IsAdmin")
        };
    }

    private static bool ReadByteFlag(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetByte(ordinal) != 0;
    }

    private static bool ReadBool(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private const string UserSelectSql = """
        SELECT u.username, u.name, u.comp_id, u.upload_doc, u.scan_doc, u.delete_doc, u.delete_manage,
               u.print_doc, u.download_doc, u.add_cat, u.add_users, u.add_patients, u.box, u.report,
               u.su, u.disabled, u.IsAdmin
        FROM usersinfo u
        """;
}
