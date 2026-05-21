using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class DocumentRepository
{
    private readonly string _connectionString;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public DocumentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<IReadOnlyList<DocumentDto>> GetByPatientAsync(int companyId, int patientId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var documents = new List<DocumentDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.doc_id, d.comp_id, d.patient_id, d.cat_id, c.cat_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.date_of_service, d.uploaded_by
            FROM documents d
            LEFT JOIN category c ON d.cat_id = c.cat_id
            WHERE d.comp_id = @companyId
              AND d.patient_id = @patientId
              AND ISNULL(d.deleted, 0) = 0
            ORDER BY d.date DESC, d.doc_id DESC;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }

        return documents;
    }

    public async Task<IReadOnlyList<DocumentDto>> GetReportAsync(int companyId, DateTime? fromDate, DateTime? toDate, int take = 500, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var documents = new List<DocumentDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take) d.doc_id, d.comp_id, d.patient_id, d.cat_id, c.cat_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.date_of_service, d.uploaded_by
            FROM documents d
            LEFT JOIN category c ON d.cat_id = c.cat_id
            WHERE d.comp_id = @companyId
              AND ISNULL(d.deleted, 0) = 0
              AND (@fromDate IS NULL OR d.date >= @fromDate)
              AND (@toDate IS NULL OR d.date < DATEADD(day, 1, @toDate))
            ORDER BY d.date DESC, d.doc_id DESC;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@take", Math.Clamp(take, 1, 2000));
        command.Parameters.AddWithValue("@fromDate", fromDate.HasValue ? fromDate.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@toDate", toDate.HasValue ? toDate.Value.Date : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }

        return documents;
    }

    public async Task<DocumentDto> CreateAsync(int companyId, int patientId, int categoryId, string fileName, string relativeUrl, int pages, string? uploadedBy, DateTime? dateOfService = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO documents (comp_id, patient_id, cat_id, doc_name, url, num_pages, date, date_of_service, uploaded_by, deleted)
            OUTPUT INSERTED.doc_id
            VALUES (@companyId, @patientId, @categoryId, @documentName, @url, @pages, @date, @dateOfService, @uploadedBy, 0);
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@patientId", patientId);
        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@documentName", fileName);
        command.Parameters.AddWithValue("@url", relativeUrl);
        command.Parameters.AddWithValue("@pages", pages);
        command.Parameters.AddWithValue("@date", DateTime.UtcNow);
        command.Parameters.AddWithValue("@dateOfService", dateOfService.HasValue ? dateOfService.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@uploadedBy", string.IsNullOrWhiteSpace(uploadedBy) ? DBNull.Value : uploadedBy.Trim());

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return await GetAsync(id, cancellationToken) ?? throw new InvalidOperationException("Document was created but could not be loaded.");
    }

    public async Task<DocumentDto?> GetDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await GetAsync(documentId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int documentId, int companyId, string? deletedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE documents
            SET deleted = 1,
                deleted_on = @deletedOn,
                deleted_by = @deletedBy
            WHERE doc_id = @documentId
              AND comp_id = @companyId;
            """;
        command.Parameters.AddWithValue("@documentId", documentId);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@deletedOn", DateTime.UtcNow);
        command.Parameters.AddWithValue("@deletedBy", string.IsNullOrWhiteSpace(deletedBy) ? DBNull.Value : deletedBy.Trim());

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<DocumentDto?> GetAsync(int documentId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.doc_id, d.comp_id, d.patient_id, d.cat_id, c.cat_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.date_of_service, d.uploaded_by
            FROM documents d
            LEFT JOIN category c ON d.cat_id = c.cat_id
            WHERE d.doc_id = @documentId;
            """;
        command.Parameters.AddWithValue("@documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapDocument(reader) : null;
    }

    private static DocumentDto MapDocument(SqlDataReader reader)
    {
        return new DocumentDto
        {
            DocumentId = reader.GetInt32(reader.GetOrdinal("doc_id")),
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            PatientId = ReadNullableInt(reader, "patient_id"),
            CategoryId = ReadNullableInt(reader, "cat_id"),
            CategoryName = ReadString(reader, "cat_name"),
            DocumentName = ReadString(reader, "doc_name"),
            Url = ReadString(reader, "url"),
            NumberOfPages = reader.GetInt32(reader.GetOrdinal("num_pages")),
            Date = reader.GetDateTime(reader.GetOrdinal("date")),
            DateOfService = ReadNullableDateTime(reader, "date_of_service"),
            UploadedBy = ReadString(reader, "uploaded_by")
        };
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaChecked)
        {
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaChecked)
            {
                return;
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF COL_LENGTH('dbo.documents', 'date_of_service') IS NULL
                BEGIN
                    ALTER TABLE dbo.documents ADD date_of_service date NULL;
                END
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaChecked = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private static int? ReadNullableInt(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? ReadNullableDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
