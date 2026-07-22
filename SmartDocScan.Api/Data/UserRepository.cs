using Npgsql;
using System.Security.Cryptography;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class UserRepository
{
    private readonly string _connectionString;
    private readonly bool _allowLegacyPlaintextPasswords;
    private readonly bool _autoEnsureSchema;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _allowLegacyPlaintextPasswords = configuration.GetValue<bool>("Authentication:AllowLegacyPlaintextPasswords");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    public async Task<IReadOnlyList<UserDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var users = new List<UserDto>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = UserSelectSql + " WHERE comp_id = @companyId ORDER BY name, username;";
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(MapUser(reader));
        }

        return users;
    }

    public async Task<UserDto?> LoginAsync(string? username, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var normalizedUsername = username.Trim();
        var loginAttemptTrackingAvailable = await TryEnsureLoginAttemptTableAsync(connection, _autoEnsureSchema, cancellationToken);
        if (loginAttemptTrackingAvailable && await IsLoginLockedAsync(connection, normalizedUsername, cancellationToken))
        {
            return null;
        }

        UserDto? user;
        string? storedPassword;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = UserSelectSqlWithPassword + """
             WHERE username = @username
               AND disabled = false;
            """;
            command.Parameters.AddWithValue("@username", normalizedUsername);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                if (loginAttemptTrackingAvailable)
                {
                    await RecordFailedLoginAsync(connection, normalizedUsername, cancellationToken);
                }
                return null;
            }

            storedPassword = ReadString(reader, "password");
            user = MapUser(reader);
        }

        if (!VerifyPassword(password.Trim(), storedPassword, _allowLegacyPlaintextPasswords, out var needsRehash))
        {
            if (loginAttemptTrackingAvailable)
            {
                await RecordFailedLoginAsync(connection, normalizedUsername, cancellationToken);
            }
            return null;
        }

        if (loginAttemptTrackingAvailable)
        {
            await ClearFailedLoginsAsync(connection, normalizedUsername, cancellationToken);
        }

        if (needsRehash)
        {
            await UpdatePasswordAsync(connection, normalizedUsername, HashPassword(password.Trim()), cancellationToken);
        }

        return user;
    }

    public async Task<UserDto> UpsertAsync(UserUpsertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Username and name are required.");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var exists = await ExistsAsync(connection, request.Username.Trim(), cancellationToken);
        var passwordProvided = !string.IsNullOrWhiteSpace(request.Password);
        var generatedPassword = !exists && !passwordProvided
            ? GenerateTemporaryPassword()
            : null;
        var passwordToSave = passwordProvided
            ? request.Password!.Trim()
            : generatedPassword;

        if (passwordProvided
            && !string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Passwords do not match.");
        }

        if (!string.IsNullOrWhiteSpace(passwordToSave) && !IsPasswordLongEnough(passwordToSave))
        {
            throw new InvalidOperationException($"Password must be at least {MinimumPasswordLength} characters.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = exists
            ? (passwordProvided ? UpdateSql : UpdateSqlWithoutPassword)
            : InsertSql;
        AddUpsertParameters(command, request, passwordToSave is not null ? HashPassword(passwordToSave) : null);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var savedUser = await GetByUsernameAsync(request.Username.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("User was saved but could not be loaded.");
        savedUser.GeneratedPassword = generatedPassword;
        return savedUser;
    }

    public async Task<bool> DeleteAsync(string username, int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM usersinfo WHERE username = @username AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@username", username.Trim());
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> ChangePasswordAsync(string username, string? currentPassword, string? newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        if (!IsPasswordLongEnough(newPassword))
        {
            return false;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var storedPassword = await GetStoredPasswordAsync(connection, username.Trim(), cancellationToken);
        if (!VerifyPassword(currentPassword.Trim(), storedPassword, _allowLegacyPlaintextPasswords, out _))
        {
            return false;
        }

        return await UpdatePasswordAsync(connection, username.Trim(), HashPassword(newPassword.Trim()), cancellationToken) > 0;
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM usersinfo WHERE username = @username;";
        command.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = UserSelectSql + " WHERE username = @username;";
        command.Parameters.AddWithValue("@username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    private static void AddUpsertParameters(NpgsqlCommand command, UserUpsertRequest request, string? passwordHash)
    {
        command.Parameters.AddWithValue("@username", request.Username!.Trim());
        command.Parameters.AddWithValue("@name", request.Name!.Trim());
        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            command.Parameters.AddWithValue("@password", passwordHash);
        }
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@uploadDoc", Flag(request.UploadDocument));
        command.Parameters.AddWithValue("@scanDoc", Flag(request.ScanDocument));
        command.Parameters.AddWithValue("@deleteDoc", Flag(request.DeleteDocument));
        command.Parameters.AddWithValue("@deleteManage", Flag(request.DeleteManage));
        command.Parameters.AddWithValue("@printDoc", Flag(request.PrintDocument));
        command.Parameters.AddWithValue("@downloadDoc", Flag(request.DownloadDocument));
        command.Parameters.AddWithValue("@addCat", Flag(request.AddCategory));
        command.Parameters.AddWithValue("@addUsers", Flag(request.AddUsers));
        command.Parameters.AddWithValue("@addPatients", Flag(request.AddPatients));
        command.Parameters.AddWithValue("@box", Flag(request.Box));
        command.Parameters.AddWithValue("@report", Flag(request.Report));
        command.Parameters.AddWithValue("@su", Flag(request.SuperUser));
        command.Parameters.AddWithValue("@disabled", Flag(request.Disabled));
        command.Parameters.AddWithValue("@isAdmin", request.IsAdmin);
    }

    private static byte Flag(bool value) => value ? (byte)1 : (byte)0;

    private static UserDto MapUser(NpgsqlDataReader reader)
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
            IsAdmin = ReadBool(reader, "isadmin")
        };
    }

    private static bool ReadByteFlag(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }

    private static bool ReadBool(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }

    private static string? ReadString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static async Task<string?> GetStoredPasswordAsync(NpgsqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT password
            FROM usersinfo
            WHERE username = @username
              AND disabled = false;
            """;
        command.Parameters.AddWithValue("@username", username);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<int> UpdatePasswordAsync(NpgsqlConnection connection, string username, string passwordHash, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE usersinfo
            SET password = @password
            WHERE username = @username
              AND disabled = false;
            """;
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@password", passwordHash);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TryEnsureLoginAttemptTableAsync(NpgsqlConnection connection, bool autoEnsureSchema, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            if (autoEnsureSchema)
            {
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS auth_login_attempt (
                        username varchar(50) PRIMARY KEY,
                        failed_count int NOT NULL,
                        first_failed_on timestamp without time zone NOT NULL,
                        last_failed_on timestamp without time zone NOT NULL,
                        locked_until timestamp without time zone NULL
                    );
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            }

            command.CommandText = "SELECT CASE WHEN to_regclass('public.auth_login_attempt') IS NULL THEN 0 ELSE 1 END;";
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
        }
        catch (PostgresException)
        {
            return false;
        }
    }

    private static async Task<bool> IsLoginLockedAsync(NpgsqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT locked_until
            FROM auth_login_attempt
            WHERE username = @username;
            """;
        command.Parameters.AddWithValue("@username", username);
        var lockedUntil = await command.ExecuteScalarAsync(cancellationToken);
        return lockedUntil is DateTime value && value > DateTime.UtcNow;
    }

    private static async Task RecordFailedLoginAsync(NpgsqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO auth_login_attempt (username, failed_count, first_failed_on, last_failed_on, locked_until)
            VALUES (@username, 1, (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'), (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'), NULL)
            ON CONFLICT (username)
            DO UPDATE SET
                failed_count = CASE
                    WHEN EXTRACT(EPOCH FROM ((CURRENT_TIMESTAMP AT TIME ZONE 'UTC') - auth_login_attempt.first_failed_on)) / 60 >= @windowMinutes THEN 1
                    ELSE auth_login_attempt.failed_count + 1
                END,
                first_failed_on = CASE
                    WHEN EXTRACT(EPOCH FROM ((CURRENT_TIMESTAMP AT TIME ZONE 'UTC') - auth_login_attempt.first_failed_on)) / 60 >= @windowMinutes THEN (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
                    ELSE auth_login_attempt.first_failed_on
                END,
                last_failed_on = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
                locked_until = CASE
                    WHEN EXTRACT(EPOCH FROM ((CURRENT_TIMESTAMP AT TIME ZONE 'UTC') - auth_login_attempt.first_failed_on)) / 60 < @windowMinutes
                         AND auth_login_attempt.failed_count + 1 >= @maxFailures
                    THEN (CURRENT_TIMESTAMP AT TIME ZONE 'UTC') + (@lockoutMinutes * INTERVAL '1 minute')
                    ELSE auth_login_attempt.locked_until
                END;
            """;
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@windowMinutes", LoginFailureWindowMinutes);
        command.Parameters.AddWithValue("@maxFailures", MaxLoginFailures);
        command.Parameters.AddWithValue("@lockoutMinutes", LoginLockoutMinutes);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ClearFailedLoginsAsync(NpgsqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM auth_login_attempt WHERE username = @username;";
        command.Parameters.AddWithValue("@username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool VerifyPassword(string password, string? storedPassword, bool allowLegacyPlaintextPasswords, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(storedPassword))
        {
            return false;
        }

        if (!storedPassword.StartsWith(PasswordHashPrefix, StringComparison.Ordinal))
        {
            needsRehash = allowLegacyPlaintextPasswords;
            return allowLegacyPlaintextPasswords && string.Equals(password, storedPassword, StringComparison.Ordinal);
        }

        var parts = storedPassword.Split('$');
        if (parts.Length != 4
            || !int.TryParse(parts[1], out var iterations)
            || iterations < 10000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordHashIterations, HashAlgorithmName.SHA256, 32);
        return $"{PasswordHashPrefix}${PasswordHashIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool IsPasswordLongEnough(string? password)
    {
        return password?.Trim().Length >= MinimumPasswordLength;
    }

    private static string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        const string all = uppercase + lowercase + digits + symbols;
        Span<char> password = stackalloc char[14];
        password[0] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
        password[1] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (var index = 4; index < password.Length; index++)
        {
            password[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        for (var index = password.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (password[index], password[swapIndex]) = (password[swapIndex], password[index]);
        }

        return new string(password);
    }

    private const string UserSelectSql = """
        SELECT username, name, comp_id, upload_doc, scan_doc, delete_doc, delete_manage,
               print_doc, download_doc, add_cat, add_users, add_patients, box, report,
               su, disabled, isadmin
        FROM usersinfo
        """;

    private const string UserSelectSqlWithPassword = """
        SELECT username, name, password, comp_id, upload_doc, scan_doc, delete_doc, delete_manage,
               print_doc, download_doc, add_cat, add_users, add_patients, box, report,
               su, disabled, isadmin
        FROM usersinfo
        """;

    private const string PasswordHashPrefix = "pbkdf2_sha256";
    private const int PasswordHashIterations = 100000;
    private const int MinimumPasswordLength = 8;
    private const int MaxLoginFailures = 10;
    private const int LoginFailureWindowMinutes = 15;
    private const int LoginLockoutMinutes = 15;

    private const string InsertSql = """
        INSERT INTO usersinfo (username, name, password, comp_id, upload_doc, scan_doc, delete_doc,
                               delete_manage, print_doc, download_doc, add_cat, add_users,
                               add_patients, box, report, su, disabled, isadmin)
        VALUES (@username, @name, @password, @companyId, @uploadDoc, @scanDoc, @deleteDoc,
                @deleteManage, @printDoc, @downloadDoc, @addCat, @addUsers,
                @addPatients, @box, @report, @su, @disabled, @isAdmin);
        """;

    private const string UpdateSql = """
        UPDATE usersinfo
        SET name = @name,
            password = @password,
            comp_id = @companyId,
            upload_doc = @uploadDoc,
            scan_doc = @scanDoc,
            delete_doc = @deleteDoc,
            delete_manage = @deleteManage,
            print_doc = @printDoc,
            download_doc = @downloadDoc,
            add_cat = @addCat,
            add_users = @addUsers,
            add_patients = @addPatients,
            box = @box,
            report = @report,
            su = @su,
            disabled = @disabled,
            isadmin = @isAdmin
        WHERE username = @username;
        """;

    private const string UpdateSqlWithoutPassword = """
        UPDATE usersinfo
        SET name = @name,
            comp_id = @companyId,
            upload_doc = @uploadDoc,
            scan_doc = @scanDoc,
            delete_doc = @deleteDoc,
            delete_manage = @deleteManage,
            print_doc = @printDoc,
            download_doc = @downloadDoc,
            add_cat = @addCat,
            add_users = @addUsers,
            add_patients = @addPatients,
            box = @box,
            report = @report,
            su = @su,
            disabled = @disabled,
            isadmin = @isAdmin
        WHERE username = @username;
        """;
}
