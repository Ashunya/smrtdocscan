using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class BoxRepository
{
    private readonly string _connectionString;

    public BoxRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<IReadOnlyList<BoxDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var boxes = new List<BoxDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1000) box_id, comp_id, box_ext_id, box_name, aisle, section, brow, bcolumn
            FROM box
            WHERE comp_id = @companyId
            ORDER BY box_id;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            boxes.Add(MapBox(reader));
        }

        return boxes;
    }

    public async Task<BoxDto> CreateAsync(BoxUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureExternalBoxIdIsUniqueAsync(request.ExternalBoxId, cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO box (comp_id, box_ext_id, box_name, aisle, section, brow, bcolumn)
            OUTPUT INSERTED.box_id
            VALUES (@companyId, @externalBoxId, @boxName, @aisle, @section, @row, @column);
            """;
        AddUpsertParameters(command, request);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return await GetAsync(id, cancellationToken) ?? throw new InvalidOperationException("Box was created but could not be loaded.");
    }

    public async Task<bool> DeleteAsync(int boxId, int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM box WHERE box_id = @boxId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@boxId", boxId);
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<BoxDto?> GetAsync(int boxId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT box_id, comp_id, box_ext_id, box_name, aisle, section, brow, bcolumn
            FROM box
            WHERE box_id = @boxId;
            """;
        command.Parameters.AddWithValue("@boxId", boxId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapBox(reader) : null;
    }

    private async Task EnsureExternalBoxIdIsUniqueAsync(int externalBoxId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM box WHERE box_ext_id = @externalBoxId;";
        command.Parameters.AddWithValue("@externalBoxId", externalBoxId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (count > 0)
        {
            throw new InvalidOperationException("Box ID already exists.");
        }
    }

    private static void AddUpsertParameters(SqlCommand command, BoxUpsertRequest request)
    {
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@externalBoxId", request.ExternalBoxId);
        command.Parameters.AddWithValue("@boxName", DbValue(request.BoxName));
        command.Parameters.AddWithValue("@aisle", DbValue(request.Aisle));
        command.Parameters.AddWithValue("@section", DbValue(request.Section));
        command.Parameters.AddWithValue("@row", DbValue(request.Row));
        command.Parameters.AddWithValue("@column", DbValue(request.Column));
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static BoxDto MapBox(SqlDataReader reader)
    {
        return new BoxDto
        {
            BoxId = reader.GetInt32(reader.GetOrdinal("box_id")),
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            ExternalBoxId = reader.GetInt32(reader.GetOrdinal("box_ext_id")),
            BoxName = ReadString(reader, "box_name"),
            Aisle = ReadString(reader, "aisle"),
            Section = ReadString(reader, "section"),
            Row = ReadString(reader, "brow"),
            Column = ReadString(reader, "bcolumn")
        };
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
