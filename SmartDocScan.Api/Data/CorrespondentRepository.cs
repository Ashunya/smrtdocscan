using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class CorrespondentRepository
{
    private readonly string _connectionString;
    private readonly bool _autoEnsureSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public CorrespondentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaChecked) return;
        if (!_autoEnsureSchema)
        {
            _schemaChecked = true;
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaChecked) return;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    IF OBJECT_ID('dbo.business_correspondents', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.business_correspondents (
                            corresp_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_business_correspondents PRIMARY KEY,
                            comp_id INT NOT NULL,
                            name NVARCHAR(255) NOT NULL,
                            match_algorithm NVARCHAR(50) NOT NULL CONSTRAINT DF_business_correspondents_match_alg DEFAULT 'any',
                            match_pattern NVARCHAR(MAX) NULL
                        );
                        CREATE INDEX IX_business_correspondents_comp ON dbo.business_correspondents(comp_id);
                    END
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            _schemaChecked = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    public async Task<IReadOnlyList<CorrespondentDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var correspondents = new List<CorrespondentDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT corresp_id, comp_id, name, match_algorithm, match_pattern
            FROM business_correspondents
            WHERE comp_id = @companyId
            ORDER BY name;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            correspondents.Add(new CorrespondentDto
            {
                CorrespondentId = reader.GetInt32(reader.GetOrdinal("corresp_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                MatchAlgorithm = reader.GetString(reader.GetOrdinal("match_algorithm")),
                MatchPattern = ReadString(reader, "match_pattern")
            });
        }
        return correspondents;
    }

    public async Task<CorrespondentDto> CreateAsync(CorrespondentUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO business_correspondents (comp_id, name, match_algorithm, match_pattern)
            OUTPUT INSERTED.corresp_id
            VALUES (@companyId, @name, @matchAlgorithm, @matchPattern);
            """;
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@name", request.Name.Trim());
        command.Parameters.AddWithValue("@matchAlgorithm", string.IsNullOrWhiteSpace(request.MatchAlgorithm) ? "any" : request.MatchAlgorithm.Trim());
        command.Parameters.AddWithValue("@matchPattern", string.IsNullOrWhiteSpace(request.MatchPattern) ? DBNull.Value : request.MatchPattern.Trim());

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Correspondent was created but could not be loaded.");
    }

    public async Task<CorrespondentDto?> GetAsync(int correspondentId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT corresp_id, comp_id, name, match_algorithm, match_pattern
            FROM business_correspondents
            WHERE corresp_id = @correspondentId;
            """;
        command.Parameters.AddWithValue("@correspondentId", correspondentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new CorrespondentDto
            {
                CorrespondentId = reader.GetInt32(reader.GetOrdinal("corresp_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                MatchAlgorithm = reader.GetString(reader.GetOrdinal("match_algorithm")),
                MatchPattern = ReadString(reader, "match_pattern")
            };
        }
        return null;
    }

    public async Task<bool> DeleteAsync(int correspondentId, int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM business_correspondents WHERE corresp_id = @correspondentId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@correspondentId", correspondentId);
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
