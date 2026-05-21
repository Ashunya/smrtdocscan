using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<IReadOnlyList<UserDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var users = new List<UserDto>();
        await using var connection = new SqlConnection(_connectionString);
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        UserDto? user;
        string? storedPassword;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = UserSelectSqlWithPassword + """
             WHERE username = @username
               AND disabled = 0;
            """;
            command.Parameters.AddWithValue("@username", username.Trim());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            storedPassword = ReadString(reader, "password");
            user = MapUser(reader);
        }

        if (!VerifyPassword(password.Trim(), storedPassword, out var needsRehash))
        {
            return null;
        }

        if (needsRehash)
        {
            await UpdatePasswordAsync(connection, username.Trim(), HashPassword(password.Trim()), cancellationToken);
        }

        return user;
    }

    public async Task<UserDto> UpsertAsync(UserUpsertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Username, name, and password are required.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var exists = await ExistsAsync(connection, request.Username.Trim(), cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = exists ? UpdateSql : InsertSql;
        AddUpsertParameters(command, request, HashPassword(request.Password!.Trim()));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetByUsernameAsync(request.Username.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("User was saved but could not be loaded.");
    }

    public async Task<bool> DeleteAsync(string username, int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var storedPassword = await GetStoredPasswordAsync(connection, username.Trim(), cancellationToken);
        if (!VerifyPassword(currentPassword.Trim(), storedPassword, out _))
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

        await using var command = connection.CreateCommand();
        command.CommandText = UserSelectSql + " WHERE username = @username;";
        command.Parameters.AddWithValue("@username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    private static void AddUpsertParameters(SqlCommand command, UserUpsertRequest request, string passwordHash)
    {
        command.Parameters.AddWithValue("@username", request.Username!.Trim());
        command.Parameters.AddWithValue("@name", request.Name!.Trim());
        command.Parameters.AddWithValue("@password", passwordHash);
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

    private static bool VerifyPassword(string password, string? storedPassword, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(storedPassword))
        {
            return false;
        }

        if (!storedPassword.StartsWith(PasswordHashPrefix, StringComparison.Ordinal))
        {
            needsRehash = true;
            return string.Equals(password, storedPassword, StringComparison.Ordinal);
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

    private const string UserSelectSql = """
        SELECT username, name, comp_id, upload_doc, scan_doc, delete_doc, delete_manage,
               print_doc, download_doc, add_cat, add_users, add_patients, box, report,
               su, disabled, IsAdmin
        FROM usersinfo
        """;

    private const string UserSelectSqlWithPassword = """
        SELECT username, name, password, comp_id, upload_doc, scan_doc, delete_doc, delete_manage,
               print_doc, download_doc, add_cat, add_users, add_patients, box, report,
               su, disabled, IsAdmin
        FROM usersinfo
        """;

    private const string PasswordHashPrefix = "pbkdf2_sha256";
    private const int PasswordHashIterations = 100000;

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
}
