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

    public async Task<IReadOnlyList<BusinessDocumentDto>> GetByCompanyAsync(int companyId, int? locationId, int? categoryId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var documents = new List<BusinessDocumentDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.doc_id, d.comp_id, d.location_id, l.location_name, d.cat_id, c.cat_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.document_date, d.vendor_name, d.amount, d.uploaded_by
            FROM business_documents d
            LEFT JOIN category c ON d.cat_id = c.cat_id
            LEFT JOIN company_location l ON d.location_id = l.location_id
            WHERE d.comp_id = @companyId
              AND (@locationId IS NULL OR d.location_id = @locationId)
              AND (@categoryId IS NULL OR d.cat_id = @categoryId)
              AND ISNULL(d.deleted, 0) = 0
            ORDER BY d.date DESC, d.doc_id DESC;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@locationId", locationId.HasValue ? locationId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@categoryId", categoryId.HasValue ? categoryId.Value : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }

        return documents;
    }

    public async Task<BusinessDocumentDto> CreateAsync(
        int companyId,
        int? locationId,
        int categoryId,
        string fileName,
        string relativeUrl,
        int pages,
        string? uploadedBy,
        DateTime? documentDate = null,
        string? vendorName = null,
        decimal? amount = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO business_documents (comp_id, location_id, cat_id, doc_name, url, num_pages, date, document_date, vendor_name, amount, uploaded_by, deleted)
            OUTPUT INSERTED.doc_id
            VALUES (@companyId, @locationId, @categoryId, @documentName, @url, @pages, @date, @documentDate, @vendorName, @amount, @uploadedBy, 0);
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@locationId", locationId.HasValue ? locationId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@documentName", fileName);
        command.Parameters.AddWithValue("@url", relativeUrl);
        command.Parameters.AddWithValue("@pages", pages);
        command.Parameters.AddWithValue("@date", DateTime.UtcNow);
        command.Parameters.AddWithValue("@documentDate", documentDate.HasValue ? documentDate.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@vendorName", string.IsNullOrWhiteSpace(vendorName) ? DBNull.Value : vendorName.Trim());
        command.Parameters.AddWithValue("@amount", amount.HasValue ? amount.Value : DBNull.Value);
        command.Parameters.AddWithValue("@uploadedBy", string.IsNullOrWhiteSpace(uploadedBy) ? DBNull.Value : uploadedBy.Trim());

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
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
            SELECT d.doc_id, d.comp_id, d.location_id, l.location_name, d.cat_id, c.cat_name, d.doc_name, d.url,
                   d.num_pages, d.date, d.document_date, d.vendor_name, d.amount, d.uploaded_by
            FROM business_documents d
            LEFT JOIN category c ON d.cat_id = c.cat_id
            LEFT JOIN company_location l ON d.location_id = l.location_id
            WHERE d.doc_id = @documentId;
            """;
        command.Parameters.AddWithValue("@documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapDocument(reader) : null;
    }

    public async Task<bool> UpdateCategoryAsync(int documentId, int categoryId, int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE business_documents
            SET cat_id = @categoryId
            WHERE doc_id = @documentId
              AND comp_id = @companyId;
            """;
        command.Parameters.AddWithValue("@documentId", documentId);
        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@companyId", companyId);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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
                            cat_id INT NOT NULL,
                            doc_name NVARCHAR(255) NOT NULL,
                            url NVARCHAR(500) NOT NULL,
                            num_pages INT NOT NULL,
                            date DATETIME2 NOT NULL CONSTRAINT DF_business_documents_date DEFAULT SYSUTCDATETIME(),
                            document_date DATE NULL,
                            vendor_name NVARCHAR(150) NULL,
                            amount DECIMAL(18,2) NULL,
                            uploaded_by NVARCHAR(100) NULL,
                            deleted BIT NOT NULL CONSTRAINT DF_business_documents_deleted DEFAULT 0,
                            deleted_on DATETIME2 NULL,
                            deleted_by NVARCHAR(100) NULL
                        );
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
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_business_documents_category' AND object_id = OBJECT_ID('dbo.business_documents'))
                    BEGIN
                        CREATE INDEX IX_business_documents_category ON dbo.business_documents(cat_id);
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
            CategoryId = reader.GetInt32(reader.GetOrdinal("cat_id")),
            CategoryName = ReadString(reader, "cat_name"),
            DocumentName = reader.IsDBNull(docNameOrdinal) ? "" : reader.GetString(docNameOrdinal),
            Url = reader.IsDBNull(urlOrdinal) ? "" : reader.GetString(urlOrdinal),
            NumberOfPages = reader.IsDBNull(numPagesOrdinal) ? 1 : reader.GetInt32(numPagesOrdinal),
            Date = reader.IsDBNull(dateOrdinal) ? DateTime.UtcNow : reader.GetDateTime(dateOrdinal),
            DocumentDate = ReadNullableDateTime(reader, "document_date"),
            VendorName = ReadString(reader, "vendor_name"),
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
