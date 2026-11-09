using Npgsql;
using NpgsqlTypes;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class DocumentRepository
{
    private readonly string _connectionString;
    private readonly bool _autoEnsureSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public DocumentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    public async Task<IReadOnlyList<DocumentDto>> GetByPatientAsync(int companyId, int patientId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var documents = new List<DocumentDto>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.doc_id, d.comp_id, d.patient_id, d.cat_id, c.cat_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.date_of_service, d.uploaded_by
            FROM documents d
            INNER JOIN patient p ON p.comp_id = d.comp_id
                                AND p.patient_id = @patientId
                                AND (
                                    d.patient_id = p.patient_id
                                    OR (
                                        p.pext_id IS NOT NULL
                                        AND btrim(p.pext_id) = d.patient_id::text
                                    )
                                )
            LEFT JOIN category c ON d.cat_id = c.cat_id
            WHERE d.comp_id = @companyId
              AND COALESCE(d.deleted, false) = false
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
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.doc_id, d.comp_id, d.patient_id, d.cat_id, c.cat_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.date_of_service, d.uploaded_by
            FROM documents d
            LEFT JOIN category c ON d.cat_id = c.cat_id
            WHERE d.comp_id = @companyId
              AND COALESCE(d.deleted, false) = false
              AND (@fromDate IS NULL OR d.date >= @fromDate)
              AND (@toDate IS NULL OR d.date < @toDate + INTERVAL '1 day')
            ORDER BY d.date DESC, d.doc_id DESC
            LIMIT @take;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@take", Math.Clamp(take, 1, 2000));
        command.Parameters.AddWithValue("@fromDate", NpgsqlDbType.Timestamp, fromDate.HasValue ? fromDate.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@toDate", NpgsqlDbType.Timestamp, toDate.HasValue ? toDate.Value.Date : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }

        return documents;
    }

    public async Task<IReadOnlyList<ScannedPagesReportRowDto>> GetScannedPagesReportAsync(int? companyId, string? userName, DateTime? fromDate, DateTime? toDate, bool groupByDay, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var rows = new List<ScannedPagesReportRowDto>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // The person who added a document is taken from the audit log when available, because
        // uploaded_by historically held placeholders ("Scanner", or a hardcoded client default).
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH doc_actor AS (
                SELECT DISTINCT ON (a.target_id) a.target_id, btrim(a.actor) AS actor
                FROM audit_log a
                WHERE a.action IN ('document.upload', 'document.scan')
                  AND a.outcome = 'success'
                  AND a.target_type = 'document'
                  AND NULLIF(btrim(a.actor), '') IS NOT NULL
                ORDER BY a.target_id, a.created_on
            ),
            docs AS (
                SELECT d.comp_id,
                       d.num_pages,
                       d.date,
                       COALESCE(
                           da.actor,
                           CASE WHEN COALESCE(btrim(d.uploaded_by), '') IN ('', 'Scanner') THEN NULL ELSE btrim(d.uploaded_by) END,
                           'Unattributed') AS user_name
                FROM documents d
                LEFT JOIN doc_actor da ON da.target_id = d.doc_id::text
                WHERE COALESCE(d.deleted, false) = false
                  AND (@companyId IS NULL OR d.comp_id = @companyId)
                  AND (@fromDate IS NULL OR d.date >= @fromDate)
                  AND (@toDate IS NULL OR d.date < @toDate + INTERVAL '1 day')
            )
            SELECT docs.comp_id,
                   c.comp_name,
                   docs.user_name,
                   CASE WHEN @groupByDay THEN docs.date::date END AS day,
                   COUNT(*)::int AS documents,
                   COALESCE(SUM(docs.num_pages), 0)::bigint AS pages
            FROM docs
            LEFT JOIN company c ON c.comp_id = docs.comp_id
            WHERE (@userName IS NULL OR lower(docs.user_name) = lower(@userName))
            GROUP BY 1, 2, 3, 4
            ORDER BY day DESC NULLS LAST, c.comp_name, pages DESC, docs.user_name
            LIMIT 5000;
            """;
        command.Parameters.AddWithValue("@companyId", NpgsqlDbType.Integer, companyId.HasValue ? companyId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@userName", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(userName) ? DBNull.Value : userName.Trim());
        command.Parameters.AddWithValue("@fromDate", NpgsqlDbType.Timestamp, fromDate.HasValue ? fromDate.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@toDate", NpgsqlDbType.Timestamp, toDate.HasValue ? toDate.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@groupByDay", NpgsqlDbType.Boolean, groupByDay);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var dayOrdinal = reader.GetOrdinal("day");
            rows.Add(new ScannedPagesReportRowDto
            {
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                CompanyName = ReadString(reader, "comp_name"),
                UserName = reader.GetString(reader.GetOrdinal("user_name")),
                Day = reader.IsDBNull(dayOrdinal) ? null : reader.GetDateTime(dayOrdinal),
                Documents = reader.GetInt32(reader.GetOrdinal("documents")),
                Pages = reader.GetInt64(reader.GetOrdinal("pages"))
            });
        }

        return rows;
    }

    public async Task<DocumentDto> CreateAsync(int companyId, int patientId, int categoryId, string fileName, string relativeUrl, int pages, string? uploadedBy, DateTime? dateOfService = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await PostgresSequenceRepair.EnsureSerialDefaultAsync(connection, "documents", "doc_id", "documents_doc_id_seq", cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO documents (comp_id, patient_id, cat_id, doc_name, url, num_pages, date, date_of_service, uploaded_by, deleted)
            VALUES (@companyId, @patientId, @categoryId, @documentName, @url, @pages, @date, @dateOfService, @uploadedBy, false)
            RETURNING doc_id;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@patientId", patientId);
        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@documentName", fileName);
        command.Parameters.AddWithValue("@url", relativeUrl);
        command.Parameters.AddWithValue("@pages", pages);
        command.Parameters.AddWithValue("@date", DateTime.UtcNow);
        command.Parameters.AddWithValue("@dateOfService", NpgsqlDbType.Date, dateOfService.HasValue ? dateOfService.Value.Date : DBNull.Value);
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
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE documents
            SET deleted = true,
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
        await using var connection = new NpgsqlConnection(_connectionString);
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

    private static DocumentDto MapDocument(NpgsqlDataReader reader)
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
                ALTER TABLE documents ADD COLUMN IF NOT EXISTS date_of_service date NULL;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaChecked = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private static int? ReadNullableInt(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? ReadNullableDateTime(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
