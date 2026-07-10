using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class CategoryRepository
{
    private readonly string _connectionString;
    private readonly bool _autoEnsureSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public CategoryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var categories = new List<CategoryDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.cat_id, c.comp_id, c.cat_name, c.access, c.parent_id, p.cat_name AS parent_name
            FROM category c
            LEFT JOIN category p ON c.parent_id = p.cat_id
            WHERE c.comp_id = @companyId
            ORDER BY c.cat_name;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new CategoryDto
            {
                CategoryId = reader.GetInt32(reader.GetOrdinal("cat_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                CategoryName = ReadString(reader, "cat_name"),
                Access = ReadString(reader, "access"),
                ParentId = ReadNullableInt(reader, "parent_id"),
                ParentName = ReadString(reader, "parent_name")
            });
        }

        return categories;
    }

    public async Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            throw new InvalidOperationException("Category name is required.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO category (cat_name, comp_id, access, parent_id)
            OUTPUT INSERTED.cat_id
            VALUES (@categoryName, @companyId, @access, @parentId);
            """;
        command.Parameters.AddWithValue("@categoryName", request.CategoryName.Trim());
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@access", string.IsNullOrWhiteSpace(request.Access) ? DBNull.Value : request.Access.Trim());
        command.Parameters.AddWithValue("@parentId", request.ParentId.HasValue ? request.ParentId.Value : DBNull.Value);
        
        var categoryId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        return await GetAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Category was created but could not be loaded.");
    }

    public async Task<bool> DeleteAsync(int categoryId, int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM category WHERE cat_id = @categoryId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<CategoryDto?> GetAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.cat_id, c.comp_id, c.cat_name, c.access, c.parent_id, p.cat_name AS parent_name
            FROM category c
            LEFT JOIN category p ON c.parent_id = p.cat_id
            WHERE c.cat_id = @categoryId;
            """;
        command.Parameters.AddWithValue("@categoryId", categoryId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CategoryDto
            {
                CategoryId = reader.GetInt32(reader.GetOrdinal("cat_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                CategoryName = ReadString(reader, "cat_name"),
                Access = ReadString(reader, "access"),
                ParentId = ReadNullableInt(reader, "parent_id"),
                ParentName = ReadString(reader, "parent_name")
            }
            : null;
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
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF COL_LENGTH('dbo.category', 'parent_id') IS NULL
                BEGIN
                    ALTER TABLE dbo.category ADD parent_id INT NULL CONSTRAINT FK_category_parent FOREIGN KEY REFERENCES dbo.category(cat_id);
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

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
