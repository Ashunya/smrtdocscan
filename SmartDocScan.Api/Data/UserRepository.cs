using Microsoft.Data.SqlClient;
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
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var usersByUsername = new Dictionary<string, UserDto>(StringComparer.OrdinalIgnoreCase);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = UserSelectSql + " WHERE comp_id = @companyId ORDER BY name, username;";
            command.Parameters.AddWithValue("@companyId", companyId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var user = MapUser(reader);
                user.LocationIds = new List<int>();
                user.DocumentTypeIds = new List<int>();
                users.Add(user);
                if (!string.IsNullOrEmpty(user.Username))
                {
                    usersByUsername[user.Username] = user;
                }
            }
        }

        if (usersByUsername.Count > 0)
        {
            await using (var locCommand = connection.CreateCommand())
            {
                locCommand.CommandText = "SELECT ul.username, ul.location_id FROM user_locations ul JOIN usersinfo u ON u.username = ul.username WHERE u.comp_id = @companyId;";
                locCommand.Parameters.AddWithValue("@companyId", companyId);
                await using var locReader = await locCommand.ExecuteReaderAsync(cancellationToken);
                while (await locReader.ReadAsync(cancellationToken))
                {
                    var username = locReader.GetString(0);
                    var locationId = locReader.GetInt32(1);
                    if (usersByUsername.TryGetValue(username, out var user) && user.LocationIds != null)
                    {
                        ((List<int>)user.LocationIds).Add(locationId);
                    }
                }
            }

            await using (var docCommand = connection.CreateCommand())
            {
                docCommand.CommandText = "SELECT udt.username, udt.document_type_id FROM user_document_types udt JOIN usersinfo u ON u.username = udt.username WHERE u.comp_id = @companyId;";
                docCommand.Parameters.AddWithValue("@companyId", companyId);
                await using var docReader = await docCommand.ExecuteReaderAsync(cancellationToken);
                while (await docReader.ReadAsync(cancellationToken))
                {
                    var username = docReader.GetString(0);
                    var documentTypeId = docReader.GetInt32(1);
                    if (usersByUsername.TryGetValue(username, out var user) && user.DocumentTypeIds != null)
                    {
                        ((List<int>)user.DocumentTypeIds).Add(documentTypeId);
                    }
                }
            }
        }

        return users;
    }

    public async Task<UserDto?> LoginAsync(string? username, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var connection = new SqlConnection(_connectionString);
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
               AND disabled = 0;
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

        await using var connection = new SqlConnection(_connectionString);
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

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = exists
                    ? (passwordProvided ? UpdateSql : UpdateSqlWithoutPassword)
                    : InsertSql;
                AddUpsertParameters(command, request, passwordToSave is not null ? HashPassword(passwordToSave) : null);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var deleteLocCommand = connection.CreateCommand())
            {
                deleteLocCommand.Transaction = transaction;
                deleteLocCommand.CommandText = "DELETE FROM user_locations WHERE username = @username;";
                deleteLocCommand.Parameters.AddWithValue("@username", request.Username.Trim());
                await deleteLocCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            if (request.LocationIds != null)
            {
                foreach (var locationId in request.LocationIds)
                {
                    await using var insertLocCommand = connection.CreateCommand();
                    insertLocCommand.Transaction = transaction;
                    insertLocCommand.CommandText = "INSERT INTO user_locations (username, location_id) VALUES (@username, @locationId);";
                    insertLocCommand.Parameters.AddWithValue("@username", request.Username.Trim());
                    insertLocCommand.Parameters.AddWithValue("@locationId", locationId);
                    await insertLocCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await using (var deleteDocCommand = connection.CreateCommand())
            {
                deleteDocCommand.Transaction = transaction;
                deleteDocCommand.CommandText = "DELETE FROM user_document_types WHERE username = @username;";
                deleteDocCommand.Parameters.AddWithValue("@username", request.Username.Trim());
                await deleteDocCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            if (request.DocumentTypeIds != null)
            {
                foreach (var documentTypeId in request.DocumentTypeIds)
                {
                    await using var insertDocCommand = connection.CreateCommand();
                    insertDocCommand.Transaction = transaction;
                    insertDocCommand.CommandText = "INSERT INTO user_document_types (username, document_type_id) VALUES (@username, @documentTypeId);";
                    insertDocCommand.Parameters.AddWithValue("@username", request.Username.Trim());
                    insertDocCommand.Parameters.AddWithValue("@documentTypeId", documentTypeId);
                    await insertDocCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var savedUser = await GetByUsernameAsync(request.Username.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("User was saved but could not be loaded.");
        savedUser.GeneratedPassword = generatedPassword;
        return savedUser;
    }

    public async Task<bool> DeleteAsync(string username, int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var trimmedUsername = username.Trim();

            await using (var locCommand = connection.CreateCommand())
            {
                locCommand.Transaction = transaction;
                locCommand.CommandText = "DELETE FROM user_locations WHERE username = @username;";
                locCommand.Parameters.AddWithValue("@username", trimmedUsername);
                await locCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var docCommand = connection.CreateCommand())
            {
                docCommand.Transaction = transaction;
                docCommand.CommandText = "DELETE FROM user_document_types WHERE username = @username;";
                docCommand.Parameters.AddWithValue("@username", trimmedUsername);
                await docCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM usersinfo WHERE username = @username AND comp_id = @companyId;";
                command.Parameters.AddWithValue("@username", trimmedUsername);
                command.Parameters.AddWithValue("@companyId", companyId);
                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

                if (rowsAffected > 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return true;
                }

                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var storedPassword = await GetStoredPasswordAsync(connection, username.Trim(), cancellationToken);
        if (!VerifyPassword(currentPassword.Trim(), storedPassword, _allowLegacyPlaintextPasswords, out _))
        {
            return false;
        }

        return await UpdatePasswordAsync(connection, username.Trim(), HashPassword(newPassword.Trim()), cancellationToken) > 0;
    }

    private static async Task<bool> ExistsAsync(SqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM usersinfo WHERE username = @username;";
        command.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        UserDto? user = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = UserSelectSql + " WHERE username = @username;";
            command.Parameters.AddWithValue("@username", username);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                user = MapUser(reader);
            }
        }

        if (user != null)
        {
            var locs = new List<int>();
            await using (var locCommand = connection.CreateCommand())
            {
                locCommand.CommandText = "SELECT location_id FROM user_locations WHERE username = @username;";
                locCommand.Parameters.AddWithValue("@username", username);
                await using var locReader = await locCommand.ExecuteReaderAsync(cancellationToken);
                while (await locReader.ReadAsync(cancellationToken))
                {
                    locs.Add(locReader.GetInt32(0));
                }
            }
            user.LocationIds = locs;

            var docs = new List<int>();
            await using (var docCommand = connection.CreateCommand())
            {
                docCommand.CommandText = "SELECT document_type_id FROM user_document_types WHERE username = @username;";
                docCommand.Parameters.AddWithValue("@username", username);
                await using var docReader = await docCommand.ExecuteReaderAsync(cancellationToken);
                while (await docReader.ReadAsync(cancellationToken))
                {
                    docs.Add(docReader.GetInt32(0));
                }
            }
            user.DocumentTypeIds = docs;
        }

        return user;
    }

    private static void AddUpsertParameters(SqlCommand command, UserUpsertRequest request, string? passwordHash)
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

    private static UserDto MapUser(SqlDataReader reader)
    {
        return new UserDto
        {
            Username = ReadString(reader, "username"),
            Name = ReadString(reader, "name"),
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            CompanyName = HasColumn(reader, "comp_name") ? ReadString(reader, "comp_name") : null,
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

    private static async Task<string?> GetStoredPasswordAsync(SqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT password
            FROM usersinfo
            WHERE username = @username
              AND disabled = 0;
            """;
        command.Parameters.AddWithValue("@username", username);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<int> UpdatePasswordAsync(SqlConnection connection, string username, string passwordHash, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE usersinfo
            SET password = @password
            WHERE username = @username
              AND disabled = 0;
            """;
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@password", passwordHash);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TryEnsureLoginAttemptTableAsync(SqlConnection connection, bool autoEnsureSchema, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            if (autoEnsureSchema)
            {
                command.CommandText = """
                    IF OBJECT_ID('dbo.auth_login_attempt', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.auth_login_attempt (
                            username varchar(50) NOT NULL CONSTRAINT PK_auth_login_attempt PRIMARY KEY,
                            failed_count int NOT NULL,
                            first_failed_on datetime2 NOT NULL,
                            last_failed_on datetime2 NOT NULL,
                            locked_until datetime2 NULL
                        );
                    END;
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            }

            command.CommandText = "SELECT CASE WHEN OBJECT_ID('dbo.auth_login_attempt', 'U') IS NULL THEN 0 ELSE 1 END;";
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private static async Task<bool> IsLoginLockedAsync(SqlConnection connection, string username, CancellationToken cancellationToken)
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

    private static async Task RecordFailedLoginAsync(SqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            MERGE auth_login_attempt AS target
            USING (SELECT @username AS username) AS source
              ON target.username = source.username
            WHEN MATCHED THEN
                UPDATE SET
                    failed_count = CASE
                        WHEN DATEDIFF(minute, first_failed_on, SYSUTCDATETIME()) >= @windowMinutes THEN 1
                        ELSE failed_count + 1
                    END,
                    first_failed_on = CASE
                        WHEN DATEDIFF(minute, first_failed_on, SYSUTCDATETIME()) >= @windowMinutes THEN SYSUTCDATETIME()
                        ELSE first_failed_on
                    END,
                    last_failed_on = SYSUTCDATETIME(),
                    locked_until = CASE
                        WHEN DATEDIFF(minute, first_failed_on, SYSUTCDATETIME()) < @windowMinutes
                             AND failed_count + 1 >= @maxFailures
                        THEN DATEADD(minute, @lockoutMinutes, SYSUTCDATETIME())
                        ELSE locked_until
                    END
            WHEN NOT MATCHED THEN
                INSERT (username, failed_count, first_failed_on, last_failed_on, locked_until)
                VALUES (@username, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL);
            """;
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@windowMinutes", LoginFailureWindowMinutes);
        command.Parameters.AddWithValue("@maxFailures", MaxLoginFailures);
        command.Parameters.AddWithValue("@lockoutMinutes", LoginLockoutMinutes);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ClearFailedLoginsAsync(SqlConnection connection, string username, CancellationToken cancellationToken)
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

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private const string UserSelectSql = """
        SELECT u.username, u.name, u.comp_id, c.comp_name, u.upload_doc, u.scan_doc, u.delete_doc, u.delete_manage,
               u.print_doc, u.download_doc, u.add_cat, u.add_users, u.add_patients, u.box, u.report,
               u.su, u.disabled, u.IsAdmin
        FROM usersinfo u
        LEFT JOIN company c ON u.comp_id = c.comp_id
        """;

    private const string UserSelectSqlWithPassword = """
        SELECT u.username, u.name, u.password, u.comp_id, c.comp_name, u.upload_doc, u.scan_doc, u.delete_doc, u.delete_manage,
               u.print_doc, u.download_doc, u.add_cat, u.add_users, u.add_patients, u.box, u.report,
               u.su, u.disabled, u.IsAdmin
        FROM usersinfo u
        LEFT JOIN company c ON u.comp_id = c.comp_id
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
                               add_patients, box, report, su, disabled, IsAdmin)
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
            IsAdmin = @isAdmin
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
            IsAdmin = @isAdmin
        WHERE username = @username;
        """;
}

