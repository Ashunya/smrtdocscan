using Npgsql;
using NpgsqlTypes;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class AuditRepository
{
    private readonly string _connectionString;
    private readonly bool _autoEnsureSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public AuditRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    public async Task LogAsync(
        string action,
        string? actor,
        int? companyId,
        string? targetType,
        string? targetId,
        string outcome,
        string? ipAddress,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit_log (action, actor, comp_id, target_type, target_id, outcome, ip_address, details)
            VALUES (@action, @actor, @companyId, @targetType, @targetId, @outcome, @ipAddress, @details);
            """;
        command.Parameters.AddWithValue("@action", Trim(action, 80));
        command.Parameters.AddWithValue("@actor", DbValue(Trim(actor, 100)));
        command.Parameters.AddWithValue("@companyId", NpgsqlDbType.Integer, companyId.HasValue ? companyId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@targetType", DbValue(Trim(targetType, 80)));
        command.Parameters.AddWithValue("@targetId", DbValue(Trim(targetId, 160)));
        command.Parameters.AddWithValue("@outcome", Trim(outcome, 30));
        command.Parameters.AddWithValue("@ipAddress", DbValue(Trim(ipAddress, 64)));
        command.Parameters.AddWithValue("@details", DbValue(Trim(details, 1000)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> SearchAsync(
        int? companyId,
        string? actor,
        string? action,
        string? outcome,
        DateTime? fromDate,
        DateTime? toDate,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var logs = new List<AuditLogDto>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT audit_id, action, actor, comp_id, target_type, target_id,
                   outcome, ip_address, details, created_on
            FROM audit_log
            WHERE (@companyId IS NULL OR comp_id = @companyId)
              AND (@actor IS NULL OR actor ILIKE '%' || @actor || '%')
              AND (@action IS NULL OR action ILIKE '%' || @action || '%')
              AND (@outcome IS NULL OR outcome = @outcome)
              AND (@fromDate IS NULL OR created_on >= @fromDate)
              AND (@toDate IS NULL OR created_on < @toDate + INTERVAL '1 day')
            ORDER BY created_on DESC, audit_id DESC
            LIMIT @take;
            """;
        command.Parameters.AddWithValue("@take", Math.Clamp(take, 1, 500));
        command.Parameters.AddWithValue("@companyId", NpgsqlDbType.Integer, companyId.HasValue ? companyId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@actor", DbValue(Trim(actor, 100)));
        command.Parameters.AddWithValue("@action", DbValue(Trim(action, 80)));
        command.Parameters.AddWithValue("@outcome", DbValue(Trim(outcome, 30)));
        command.Parameters.AddWithValue("@fromDate", NpgsqlDbType.Timestamp, fromDate.HasValue ? fromDate.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@toDate", NpgsqlDbType.Timestamp, toDate.HasValue ? toDate.Value.Date : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(new AuditLogDto
            {
                AuditId = reader.GetInt64(reader.GetOrdinal("audit_id")),
                Action = ReadString(reader, "action"),
                Actor = ReadString(reader, "actor"),
                CompanyId = ReadNullableInt(reader, "comp_id"),
                TargetType = ReadString(reader, "target_type"),
                TargetId = ReadString(reader, "target_id"),
                Outcome = ReadString(reader, "outcome"),
                IpAddress = ReadString(reader, "ip_address"),
                Details = ReadString(reader, "details"),
                CreatedOn = reader.GetDateTime(reader.GetOrdinal("created_on"))
            });
        }

        return logs;
    }

    public async Task<int> DeleteOlderThanAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        if (retentionDays <= 0)
        {
            return 0;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM audit_log
            WHERE created_on < (CURRENT_TIMESTAMP AT TIME ZONE 'UTC') - (@retentionDays * INTERVAL '1 day');
            """;
        command.Parameters.AddWithValue("@retentionDays", retentionDays);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? ReadString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaChecked)
        {
            return;
        }

        if (!_autoEnsureSchema)
        {
            _schemaChecked = true;
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaChecked)
            {
                return;
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS audit_log (
                    audit_id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    action varchar(80) NOT NULL,
                    actor varchar(100) NULL,
                    comp_id int NULL,
                    target_type varchar(80) NULL,
                    target_id varchar(160) NULL,
                    outcome varchar(30) NOT NULL,
                    ip_address varchar(64) NULL,
                    details varchar(1000) NULL,
                    created_on timestamp without time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
                );

                CREATE INDEX IF NOT EXISTS ix_audit_log_created_on ON audit_log(created_on DESC);
                CREATE INDEX IF NOT EXISTS ix_audit_log_actor ON audit_log(actor, created_on DESC);
                CREATE INDEX IF NOT EXISTS ix_audit_log_company ON audit_log(comp_id, created_on DESC);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaChecked = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
