using Npgsql;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class CategoryRepository
{
    private readonly string _connectionString;

    public CategoryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<IReadOnlyList<CategoryDto>> GetByCompanyAsync(int companyId, string? categoryType = null, CancellationToken cancellationToken = default)
    {
        var categories = new List<CategoryDto>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.cat_id, c.comp_id, c.cat_name, c.access, c.parent_id, p.cat_name AS parent_name, c.category_type
            FROM category c
            LEFT JOIN category p ON c.parent_id = p.cat_id
            WHERE c.comp_id = @companyId
              AND (@categoryType IS NULL OR c.category_type = @categoryType)
            ORDER BY c.cat_name;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@categoryType", string.IsNullOrWhiteSpace(categoryType) ? DBNull.Value : categoryType.Trim());

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
                ParentName = ReadString(reader, "parent_name"),
                CategoryType = ReadString(reader, "category_type") ?? "patient"
            });
        }

        return categories;
    }

    public async Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            throw new InvalidOperationException("Category name is required.");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO category (cat_name, comp_id, access, parent_id, category_type)
            VALUES (@categoryName, @companyId, @access, @parentId, @categoryType)
            RETURNING cat_id;
            """;
        command.Parameters.AddWithValue("@categoryName", request.CategoryName.Trim());
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@access", string.IsNullOrWhiteSpace(request.Access) ? DBNull.Value : request.Access.Trim());
        command.Parameters.AddWithValue("@parentId", request.ParentId.HasValue ? request.ParentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@categoryType", string.IsNullOrWhiteSpace(request.CategoryType) ? "patient" : request.CategoryType.Trim());
        
        var categoryId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        return await GetAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Category was created but could not be loaded.");
    }

    public async Task<bool> DeleteAsync(int categoryId, int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM category WHERE cat_id = @categoryId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> RenameAsync(int categoryId, string newName, int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE category SET cat_name = @newName WHERE cat_id = @categoryId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@newName", newName.Trim());
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<CategoryDto?> GetAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.cat_id, c.comp_id, c.cat_name, c.access, c.parent_id, p.cat_name AS parent_name, c.category_type
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
                ParentName = ReadString(reader, "parent_name"),
                CategoryType = ReadString(reader, "category_type") ?? "patient"
            }
            : null;
    }

    private static string? ReadString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
