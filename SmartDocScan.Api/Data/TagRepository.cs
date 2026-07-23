using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class TagRepository
{
    private readonly string _connectionString;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public TagRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaChecked) return;

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaChecked) return;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    IF OBJECT_ID('dbo.business_tags', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.business_tags (
                            tag_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_business_tags PRIMARY KEY,
                            comp_id INT NOT NULL,
                            name NVARCHAR(255) NOT NULL,
                            color NVARCHAR(50) NOT NULL CONSTRAINT DF_business_tags_color DEFAULT '#c1692a',
                            match_algorithm NVARCHAR(50) NOT NULL CONSTRAINT DF_business_tags_match_alg DEFAULT 'any',
                            match_pattern NVARCHAR(MAX) NULL
                        );
                        CREATE INDEX IX_business_tags_comp ON dbo.business_tags(comp_id);
                    END

                    IF OBJECT_ID('dbo.business_document_tags', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.business_document_tags (
                            doc_id INT NOT NULL,
                            tag_id INT NOT NULL,
                            CONSTRAINT PK_business_document_tags PRIMARY KEY (doc_id, tag_id)
                        );
                        CREATE INDEX IX_business_document_tags_tag ON dbo.business_document_tags(tag_id);
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

    public async Task<IReadOnlyList<TagDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var tags = new List<TagDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tag_id, comp_id, name, color, match_algorithm, match_pattern
            FROM business_tags
            WHERE comp_id = @companyId
            ORDER BY name;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(new TagDto
            {
                TagId = reader.GetInt32(reader.GetOrdinal("tag_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Color = reader.GetString(reader.GetOrdinal("color")),
                MatchAlgorithm = reader.GetString(reader.GetOrdinal("match_algorithm")),
                MatchPattern = ReadString(reader, "match_pattern")
            });
        }
        return tags;
    }

    public async Task<TagDto> CreateAsync(TagUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO business_tags (comp_id, name, color, match_algorithm, match_pattern)
            OUTPUT INSERTED.tag_id
            VALUES (@companyId, @name, @color, @matchAlgorithm, @matchPattern);
            """;
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@name", request.Name.Trim());
        command.Parameters.AddWithValue("@color", string.IsNullOrWhiteSpace(request.Color) ? "#c1692a" : request.Color.Trim());
        command.Parameters.AddWithValue("@matchAlgorithm", string.IsNullOrWhiteSpace(request.MatchAlgorithm) ? "any" : request.MatchAlgorithm.Trim());
        command.Parameters.AddWithValue("@matchPattern", string.IsNullOrWhiteSpace(request.MatchPattern) ? DBNull.Value : request.MatchPattern.Trim());

        var tagId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        return await GetAsync(tagId, cancellationToken)
            ?? throw new InvalidOperationException("Tag was created but could not be loaded.");
    }

    public async Task<TagDto?> GetAsync(int tagId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tag_id, comp_id, name, color, match_algorithm, match_pattern
            FROM business_tags
            WHERE tag_id = @tagId;
            """;
        command.Parameters.AddWithValue("@tagId", tagId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new TagDto
            {
                TagId = reader.GetInt32(reader.GetOrdinal("tag_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Color = reader.GetString(reader.GetOrdinal("color")),
                MatchAlgorithm = reader.GetString(reader.GetOrdinal("match_algorithm")),
                MatchPattern = ReadString(reader, "match_pattern")
            };
        }
        return null;
    }

    public async Task<bool> DeleteAsync(int tagId, int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM business_document_tags WHERE tag_id = @tagId; DELETE FROM business_tags WHERE tag_id = @tagId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@tagId", tagId);
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
