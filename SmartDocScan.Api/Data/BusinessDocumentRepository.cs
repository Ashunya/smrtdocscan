using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class BusinessDocumentRepository
{
    private readonly string _connectionString;
    private readonly bool _autoEnsureSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public BusinessDocumentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    public async Task<IReadOnlyList<BusinessDocumentDto>> GetByCompanyAsync(int companyId, int? locationId, int? documentTypeId, int? correspondentId, int? tagId, string? search, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var documents = new List<BusinessDocumentDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.doc_id, d.comp_id, d.location_id, l.location_name, d.doc_type_id, dt.name AS doc_type_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.document_date, d.corresp_id, co.name AS corresp_name, d.amount, d.uploaded_by, d.asn, d.title, d.content
            FROM business_documents d
            LEFT JOIN business_document_types dt ON d.doc_type_id = dt.doc_type_id
            LEFT JOIN business_correspondents co ON d.corresp_id = co.corresp_id
            LEFT JOIN company_location l ON d.location_id = l.location_id
            WHERE d.comp_id = @companyId
              AND (@locationId IS NULL OR d.location_id = @locationId)
              AND (@documentTypeId IS NULL OR d.doc_type_id = @documentTypeId)
              AND (@correspondentId IS NULL OR d.corresp_id = @correspondentId)
              AND (@tagId IS NULL OR EXISTS(SELECT 1 FROM business_document_tags t WHERE t.doc_id = d.doc_id AND t.tag_id = @tagId))
              AND (@search IS NULL OR d.title LIKE '%' + @search + '%' OR d.doc_name LIKE '%' + @search + '%' OR d.content LIKE '%' + @search + '%')
              AND ISNULL(d.deleted, 0) = 0
            ORDER BY d.date DESC, d.doc_id DESC;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@locationId", locationId.HasValue ? locationId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@documentTypeId", documentTypeId.HasValue ? documentTypeId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@correspondentId", correspondentId.HasValue ? correspondentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@tagId", tagId.HasValue ? tagId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }
        await reader.CloseAsync();
        
        if (documents.Count > 0)
        {
            await PopulateTagsAsync(documents, connection, cancellationToken);
        }

        return documents;
    }

    public async Task<BusinessDocumentDto> CreateAsync(
        int companyId,
        int? locationId,
        int? documentTypeId,
        string fileName,
        string relativeUrl,
        int pages,
        string? uploadedBy,
        DateTime? documentDate = null,
        int? correspondentId = null,
        decimal? amount = null,
        List<int>? tagIds = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO business_documents (comp_id, location_id, doc_type_id, doc_name, url, num_pages, date, document_date, amount, uploaded_by, deleted, corresp_id)
            OUTPUT INSERTED.doc_id
            VALUES (@companyId, @locationId, @documentTypeId, @documentName, @url, @pages, @date, @documentDate, @amount, @uploadedBy, 0, @correspId);
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@locationId", locationId.HasValue ? locationId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@documentTypeId", documentTypeId.HasValue ? documentTypeId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@documentName", fileName);
        command.Parameters.AddWithValue("@url", relativeUrl);
        command.Parameters.AddWithValue("@pages", pages);
        command.Parameters.AddWithValue("@date", DateTime.UtcNow);
        command.Parameters.AddWithValue("@documentDate", documentDate.HasValue ? documentDate.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@amount", amount.HasValue ? amount.Value : DBNull.Value);
        command.Parameters.AddWithValue("@uploadedBy", string.IsNullOrWhiteSpace(uploadedBy) ? DBNull.Value : uploadedBy.Trim());
        command.Parameters.AddWithValue("@correspId", correspondentId.HasValue ? correspondentId.Value : DBNull.Value);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        if (tagIds != null && tagIds.Count > 0)
        {
            await UpdateDocumentTagsAsync(id, tagIds, cancellationToken);
        }
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Business document was created but could not be loaded.");
    }

    public async Task<bool> DeleteAsync(int documentId, int companyId, string? deletedBy, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE business_documents
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

    public async Task<BusinessDocumentDto?> GetAsync(int documentId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.doc_id, d.comp_id, d.location_id, l.location_name, d.doc_type_id, dt.name AS doc_type_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.document_date, d.corresp_id, co.name AS corresp_name, d.amount, d.uploaded_by, d.asn, d.title, d.content
            FROM business_documents d
            LEFT JOIN business_document_types dt ON d.doc_type_id = dt.doc_type_id
            LEFT JOIN business_correspondents co ON d.corresp_id = co.corresp_id
            LEFT JOIN company_location l ON d.location_id = l.location_id
            WHERE d.doc_id = @documentId;
            """;
        command.Parameters.AddWithValue("@documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var doc = await reader.ReadAsync(cancellationToken) ? MapDocument(reader) : null;
        await reader.CloseAsync();
        
        if (doc != null)
        {
            await PopulateTagsAsync(new List<BusinessDocumentDto> { doc }, connection, cancellationToken);
        }
        return doc;
    }

    public async Task<bool> UpdateMetadataAsync(int documentId, int companyId, int? documentTypeId, int? correspondentId, int? asn, string? title, decimal? amount, DateTime? documentDate, List<int>? tagIds, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE business_documents
            SET doc_type_id = @documentTypeId,
                corresp_id = @correspondentId,
                asn = @asn,
                title = @title,
                amount = @amount,
                document_date = @documentDate
            WHERE doc_id = @documentId
              AND comp_id = @companyId;
            """;
        command.Parameters.AddWithValue("@documentId", documentId);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@documentTypeId", documentTypeId.HasValue ? documentTypeId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@correspondentId", correspondentId.HasValue ? correspondentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@asn", asn.HasValue ? asn.Value : DBNull.Value);
        command.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(title) ? DBNull.Value : title.Trim());
        command.Parameters.AddWithValue("@amount", amount.HasValue ? amount.Value : DBNull.Value);
        command.Parameters.AddWithValue("@documentDate", documentDate.HasValue ? documentDate.Value.Date : DBNull.Value);

        var success = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        
        if (success && tagIds != null)
        {
            await UpdateDocumentTagsAsync(documentId, tagIds, cancellationToken);
        }

        return success;
    }

    private async Task PopulateTagsAsync(List<BusinessDocumentDto> documents, SqlConnection connection, CancellationToken cancellationToken)
    {
        var docIds = string.Join(",", documents.Select(d => d.DocumentId));
        if (string.IsNullOrEmpty(docIds)) return;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT dt.doc_id, t.tag_id, t.comp_id, t.name, t.color, t.match_algorithm, t.match_pattern
            FROM business_document_tags dt
            INNER JOIN business_tags t ON dt.tag_id = t.tag_id
            WHERE dt.doc_id IN ({docIds});
            """;
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var docId = reader.GetInt32(reader.GetOrdinal("doc_id"));
            var doc = documents.FirstOrDefault(d => d.DocumentId == docId);
            if (doc != null)
            {
                doc.Tags.Add(new TagDto
                {
                    TagId = reader.GetInt32(reader.GetOrdinal("tag_id")),
                    CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Color = reader.GetString(reader.GetOrdinal("color")),
                    MatchAlgorithm = reader.GetString(reader.GetOrdinal("match_algorithm")),
                    MatchPattern = reader.IsDBNull(reader.GetOrdinal("match_pattern")) ? null : reader.GetString(reader.GetOrdinal("match_pattern"))
                });
            }
        }
    }

    private async Task UpdateDocumentTagsAsync(int documentId, List<int> tagIds, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM business_document_tags WHERE doc_id = @documentId;";
        deleteCmd.Parameters.AddWithValue("@documentId", documentId);
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

        if (tagIds.Count > 0)
        {
            foreach (var tagId in tagIds)
            {
                await using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = "INSERT INTO business_document_tags (doc_id, tag_id) VALUES (@documentId, @tagId);";
                insertCmd.Parameters.AddWithValue("@documentId", documentId);
                insertCmd.Parameters.AddWithValue("@tagId", tagId);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
    public async Task<bool> RenameAsync(int documentId, string newName, int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE business_documents
            SET doc_name = @newName
            WHERE doc_id = @documentId
              AND comp_id = @companyId;
            """;
        command.Parameters.AddWithValue("@documentId", documentId);
        command.Parameters.AddWithValue("@newName", newName.Trim());
        command.Parameters.AddWithValue("@companyId", companyId);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    IF OBJECT_ID('dbo.business_documents', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.business_documents (
                            doc_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_business_documents PRIMARY KEY,
                            comp_id INT NOT NULL,
                            location_id INT NULL,
                            doc_type_id INT NULL,
                            doc_name NVARCHAR(255) NOT NULL,
                            title NVARCHAR(255) NULL,
                            url NVARCHAR(500) NOT NULL,
                            num_pages INT NOT NULL,
                            date DATETIME2 NOT NULL CONSTRAINT DF_business_documents_date DEFAULT SYSUTCDATETIME(),
                            document_date DATE NULL,
                            corresp_id INT NULL,
                            asn INT NULL,
                            content NVARCHAR(MAX) NULL,
                            amount DECIMAL(18,2) NULL,
                            uploaded_by NVARCHAR(100) NULL,
                            deleted BIT NOT NULL CONSTRAINT DF_business_documents_deleted DEFAULT 0,
                            deleted_on DATETIME2 NULL,
                            deleted_by NVARCHAR(100) NULL
                        );
                    END

                    IF COL_LENGTH('dbo.business_documents', 'doc_type_id') IS NULL
                    BEGIN
                        EXEC('ALTER TABLE dbo.business_documents ADD doc_type_id INT NULL;');
                    END
                    IF COL_LENGTH('dbo.business_documents', 'corresp_id') IS NULL
                    BEGIN
                        EXEC('ALTER TABLE dbo.business_documents ADD corresp_id INT NULL;');
                    END
                    IF COL_LENGTH('dbo.business_documents', 'asn') IS NULL
                    BEGIN
                        EXEC('ALTER TABLE dbo.business_documents ADD asn INT NULL;');
                    END
                    IF COL_LENGTH('dbo.business_documents', 'content') IS NULL
                    BEGIN
                        EXEC('ALTER TABLE dbo.business_documents ADD content NVARCHAR(MAX) NULL;');
                    END
                    IF COL_LENGTH('dbo.business_documents', 'title') IS NULL
                    BEGIN
                        EXEC('ALTER TABLE dbo.business_documents ADD title NVARCHAR(255) NULL;');
                    END

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

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_business_documents_comp' AND object_id = OBJECT_ID('dbo.business_documents'))
                    BEGIN
                        CREATE INDEX IX_business_documents_comp ON dbo.business_documents(comp_id);
                    END
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_business_documents_location' AND object_id = OBJECT_ID('dbo.business_documents'))
                    BEGIN
                        CREATE INDEX IX_business_documents_location ON dbo.business_documents(location_id);
                    END
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_business_documents_doc_type' AND object_id = OBJECT_ID('dbo.business_documents'))
                    BEGIN
                        CREATE INDEX IX_business_documents_doc_type ON dbo.business_documents(doc_type_id);
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

    private static BusinessDocumentDto MapDocument(SqlDataReader reader)
    {
        var docNameOrdinal = reader.GetOrdinal("doc_name");
        var urlOrdinal = reader.GetOrdinal("url");
        var numPagesOrdinal = reader.GetOrdinal("num_pages");
        var dateOrdinal = reader.GetOrdinal("date");

        return new BusinessDocumentDto
        {
            DocumentId = reader.GetInt32(reader.GetOrdinal("doc_id")),
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            LocationId = ReadNullableInt(reader, "location_id"),
            LocationName = ReadString(reader, "location_name"),
            DocumentTypeId = ReadNullableInt(reader, "doc_type_id"),
            DocumentTypeName = ReadString(reader, "doc_type_name"),
            DocumentName = reader.IsDBNull(docNameOrdinal) ? "" : reader.GetString(docNameOrdinal),
            Title = ReadString(reader, "title"),
            Url = reader.IsDBNull(urlOrdinal) ? "" : reader.GetString(urlOrdinal),
            NumberOfPages = reader.IsDBNull(numPagesOrdinal) ? 1 : reader.GetInt32(numPagesOrdinal),
            Date = reader.IsDBNull(dateOrdinal) ? DateTime.UtcNow : reader.GetDateTime(dateOrdinal),
            DocumentDate = ReadNullableDateTime(reader, "document_date"),
            CorrespondentId = ReadNullableInt(reader, "corresp_id"),
            CorrespondentName = ReadString(reader, "corresp_name"),
            ArchiveSerialNumber = ReadNullableInt(reader, "asn"),
            Content = ReadString(reader, "content"),
            Amount = ReadNullableDecimal(reader, "amount"),
            UploadedBy = ReadString(reader, "uploaded_by")
        };
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

    private static decimal? ReadNullableDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }
}
