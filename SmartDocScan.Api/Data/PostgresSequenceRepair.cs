using Npgsql;

namespace SmartDocScan.Api.Data;

internal static class PostgresSequenceRepair
{
    private static readonly SemaphoreSlim RepairLock = new(1, 1);
    private static readonly HashSet<string> RepairedSequences = new(StringComparer.OrdinalIgnoreCase);

    public static async Task EnsureSerialDefaultAsync(
        NpgsqlConnection connection,
        string tableName,
        string columnName,
        string sequenceName,
        CancellationToken cancellationToken)
    {
        if (RepairedSequences.Contains(sequenceName))
        {
            return;
        }

        await RepairLock.WaitAsync(cancellationToken);
        try
        {
            if (RepairedSequences.Contains(sequenceName))
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE SEQUENCE IF NOT EXISTS {sequenceName};
                ALTER SEQUENCE {sequenceName} OWNED BY {tableName}.{columnName};
                SELECT setval('{sequenceName}', COALESCE((SELECT MAX({columnName}) FROM {tableName}), 0) + 1, false);
                ALTER TABLE {tableName} ALTER COLUMN {columnName} SET DEFAULT nextval('{sequenceName}');
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            RepairedSequences.Add(sequenceName);
        }
        finally
        {
            RepairLock.Release();
        }
    }
}
