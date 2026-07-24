using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class DocumentTypeRepository
{
    private readonly string _connectionString;
    private readonly bool _autoEnsureSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public DocumentTypeRepository(IConfiguration configuration)
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
                    IF OBJECT_ID('dbo.business_document_types', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.business_document_types (
                            doc_type_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_business_document_types PRIMARY KEY,
                            comp_id INT NOT NULL,
                            name NVARCHAR(255) NOT NULL,
                            match_algorithm NVARCHAR(50) NOT NULL CONSTRAINT DF_business_document_types_match_alg DEFAULT 'any',
                            match_pattern NVARCHAR(MAX) NULL
                        );
                        CREATE INDEX IX_business_document_types_comp ON dbo.business_document_types(comp_id);
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

    public async Task<IReadOnlyList<DocumentTypeDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var types = new List<DocumentTypeDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT doc_type_id, comp_id, name, match_algorithm, match_pattern
            FROM business_document_types
            WHERE comp_id = @companyId
            ORDER BY name;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            types.Add(new DocumentTypeDto
            {
                DocumentTypeId = reader.GetInt32(reader.GetOrdinal("doc_type_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                MatchAlgorithm = reader.GetString(reader.GetOrdinal("match_algorithm")),
                MatchPattern = ReadString(reader, "match_pattern")
            });
        }
        return types;
    }

    public async Task<DocumentTypeDto> CreateAsync(DocumentTypeUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO business_document_types (comp_id, name, match_algorithm, match_pattern)
            OUTPUT INSERTED.doc_type_id
            VALUES (@companyId, @name, @matchAlgorithm, @matchPattern);
            """;
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@name", request.Name.Trim());
        command.Parameters.AddWithValue("@matchAlgorithm", string.IsNullOrWhiteSpace(request.MatchAlgorithm) ? "any" : request.MatchAlgorithm.Trim());
        command.Parameters.AddWithValue("@matchPattern", string.IsNullOrWhiteSpace(request.MatchPattern) ? DBNull.Value : request.MatchPattern.Trim());

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Document type was created but could not be loaded.");
    }

    public async Task<DocumentTypeDto?> GetAsync(int documentTypeId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT doc_type_id, comp_id, name, match_algorithm, match_pattern
            FROM business_document_types
            WHERE doc_type_id = @documentTypeId;
            """;
        command.Parameters.AddWithValue("@documentTypeId", documentTypeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new DocumentTypeDto
            {
                DocumentTypeId = reader.GetInt32(reader.GetOrdinal("doc_type_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                MatchAlgorithm = reader.GetString(reader.GetOrdinal("match_algorithm")),
                MatchPattern = ReadString(reader, "match_pattern")
            };
        }
        return null;
    }

    public async Task<bool> DeleteAsync(int documentTypeId, int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM business_document_types WHERE doc_type_id = @documentTypeId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@documentTypeId", documentTypeId);
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
